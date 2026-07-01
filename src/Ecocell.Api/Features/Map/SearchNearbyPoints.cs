using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Features.Map;

public static class SearchNearbyPoints
{
    public const double DefaultRadiusKm = 10;
    public const double MinRadiusKm = 1;
    public const double MaxRadiusKm = 50;

    public record Command : IRequest<ResultT<IReadOnlyList<ResponseNearbyPoint>>>
    {
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public double? RadiusKm { get; init; }
        public string? City { get; init; }

        public bool IsProximityMode => Latitude.HasValue && Longitude.HasValue;
        public bool IsCityMode => !string.IsNullOrWhiteSpace(City);
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x)
                .Must(c => c.IsProximityMode ^ c.IsCityMode)
                .WithMessage("Informe coordenadas (latitude e longitude) ou uma cidade, mas não ambos.");

            When(x => x.Latitude.HasValue || x.Longitude.HasValue, () =>
            {
                RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90m, 90m)
                    .WithMessage("Latitude deve estar entre -90 e 90.");
                RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180m, 180m)
                    .WithMessage("Longitude deve estar entre -180 e 180.");
            });

            When(x => x.RadiusKm.HasValue, () =>
            {
                RuleFor(x => x.RadiusKm!.Value).InclusiveBetween(MinRadiusKm, MaxRadiusKm)
                    .WithMessage($"O raio deve estar entre {MinRadiusKm} e {MaxRadiusKm} km.");
            });
        }
    }

    public sealed class Handler : IRequestHandler<Command, ResultT<IReadOnlyList<ResponseNearbyPoint>>>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;

        public Handler(AppDbContext dbContext, ILogger<Handler> logger, IValidator<Command> validator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
        }

        public async ValueTask<ResultT<IReadOnlyList<ResponseNearbyPoint>>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validation = _validator.Validate(request);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return ResultT<IReadOnlyList<ResponseNearbyPoint>>.Failure(Error.ErrorOnValidation(errors));
            }

            if (request.IsCityMode)
            {
                var city = request.City!.Trim().ToLower();

                var points = await _dbContext.LegalPeople
                    .Where(lp => lp.Journey == Journey.CollectPoint
                        && lp.PersonStatus == PersonStatus.Active
                        && lp.Address != null
                        && lp.Address.Latitude != null
                        && lp.Address.Longitude != null
                        && lp.Address.City.ToLower() == city)
                    .Select(lp => new ResponseNearbyPoint
                    {
                        Id = lp.Id,
                        TradeName = lp.TradeName,
                        Street = lp.Address!.Street,
                        Number = lp.Address.Number,
                        Neighborhood = lp.Address.Neighborhood,
                        City = lp.Address.City,
                        State = lp.Address.State,
                        Latitude = lp.Address.Latitude!.Value,
                        Longitude = lp.Address.Longitude!.Value,
                        DistanceKm = null
                    })
                    .ToListAsync(cancellationToken);

                return ResultT<IReadOnlyList<ResponseNearbyPoint>>.Success(points);
            }

            var lat = (double)request.Latitude!.Value;
            var lng = (double)request.Longitude!.Value;
            var radius = request.RadiusKm ?? DefaultRadiusKm;
            var collectPoint = (int)Journey.CollectPoint;
            var active = (int)PersonStatus.Active;

            FormattableString sql = $"""
                SELECT
                    lp."PersonId"    AS "Id",
                    lp."TradeName"   AS "TradeName",
                    a."Street"       AS "Street",
                    a."Number"       AS "Number",
                    a."Neighborhood" AS "Neighborhood",
                    a."City"         AS "City",
                    a."State"        AS "State",
                    a."Latitude"     AS "Latitude",
                    a."Longitude"    AS "Longitude",
                    (6371 * acos(LEAST(1.0, GREATEST(-1.0,
                        cos(radians({lat})) * cos(radians(a."Latitude"::double precision)) *
                        cos(radians(a."Longitude"::double precision) - radians({lng})) +
                        sin(radians({lat})) * sin(radians(a."Latitude"::double precision))
                    )))) AS "DistanceKm"
                FROM "LegalPeople" lp
                INNER JOIN "People" p ON p."PersonId" = lp."PersonId"
                INNER JOIN "Addresses" a ON a."Id" = lp."AddressId"
                WHERE p."Journey" = {collectPoint}
                  AND p."PersonStatus" = {active}
                  AND a."Latitude" IS NOT NULL
                  AND a."Longitude" IS NOT NULL
                  AND (6371 * acos(LEAST(1.0, GREATEST(-1.0,
                        cos(radians({lat})) * cos(radians(a."Latitude"::double precision)) *
                        cos(radians(a."Longitude"::double precision) - radians({lng})) +
                        sin(radians({lat})) * sin(radians(a."Latitude"::double precision))
                    )))) <= {radius}
                ORDER BY "DistanceKm"
                """;

            var rows = await _dbContext.Set<NearbyPointRow>()
                .FromSqlInterpolated(sql)
                .ToListAsync(cancellationToken);

            var result = rows
                .Select(r => new ResponseNearbyPoint
                {
                    Id = r.Id,
                    TradeName = r.TradeName,
                    Street = r.Street,
                    Number = r.Number,
                    Neighborhood = r.Neighborhood,
                    City = r.City,
                    State = r.State,
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    DistanceKm = r.DistanceKm
                })
                .ToList();

            return ResultT<IReadOnlyList<ResponseNearbyPoint>>.Success(result);
        }
    }
}

/// <summary>Linha de leitura da busca por proximidade (keyless). Não gera tabela/migration.</summary>
public class NearbyPointRow
{
    public Guid Id { get; set; }
    public string TradeName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public double DistanceKm { get; set; }
}

public class NearbyPointRowConfiguration : IEntityTypeConfiguration<NearbyPointRow>
{
    public void Configure(EntityTypeBuilder<NearbyPointRow> builder)
    {
        builder.HasNoKey();
        builder.ToView(null);
    }
}

/// <summary>
/// Endpoint Carter para GET /api/map/nearby — busca Pontos de Coleta ativos por proximidade ou cidade.
/// </summary>
public class SearchNearbyPointsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("api/map/nearby", async (
            [FromQuery] decimal? latitude,
            [FromQuery] decimal? longitude,
            [FromQuery] double? radiusKm,
            [FromQuery] string? city,
            ISender sender) =>
        {
            var command = new SearchNearbyPoints.Command
            {
                Latitude = latitude,
                Longitude = longitude,
                RadiusKm = radiusKm,
                City = city
            };

            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("Map")
        .WithName("SearchNearbyPoints")
        .WithSummary("Busca Pontos de Coleta ativos por proximidade (coordenadas + raio) ou por cidade.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces<List<ResponseNearbyPoint>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

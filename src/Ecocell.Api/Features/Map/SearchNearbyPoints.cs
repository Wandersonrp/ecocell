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

            throw new NotImplementedException("Modo proximidade — Task 4.");
        }
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

using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses.Discards;
using Ecocell.Shared.Utils;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecocell.Api.Features.Discard;

public static class PreviewDiscardCollectorPoint
{
    public sealed record Query(string QrCode) : IRequest<ResultT<ResponseDiscardPreviewJson>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(value => value.QrCode)
            .NotEmpty()
            .Must(value => CollectorPointQrCode.TryParse(value, out _))
            .WithMessage("O QR Code do Ponto de Coleta é inválido.");
    }

    public sealed class Handler(
        AppDbContext dbContext,
        ILogger<Handler> logger,
        IValidator<Query> validator,
        ICurrentUserService currentUserService)
        : IRequestHandler<Query, ResultT<ResponseDiscardPreviewJson>>
    {
        public async ValueTask<ResultT<ResponseDiscardPreviewJson>> Handle(Query request, CancellationToken ct)
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return ResultT<ResponseDiscardPreviewJson>.Failure(
                    Error.ErrorOnValidation(validation.Errors.Select(value => value.ErrorMessage).ToList()));
            }

            CollectorPointQrCode.TryParse(request.QrCode, out var collectorPointId);

            var currentUser = await currentUserService.GetCurrentUserAsync(ct);
            if (currentUser is null
                || currentUser.PersonType != PersonType.NaturalPerson
                || currentUser.PersonStatus != PersonStatus.Active
                || currentUser.Journey != Journey.Depositor)
            {
                logger.LogWarning("Preview de descarte negado para o Ponto de Coleta {CollectorPointId}.", collectorPointId);
                return ResultT<ResponseDiscardPreviewJson>.Failure(Error.Forbidden());
            }

            var collectorPoint = await dbContext.LegalPeople
                .AsNoTracking()
                .Include(value => value.Address)
                .FirstOrDefaultAsync(
                    value => value.Id == collectorPointId && value.Journey == Journey.CollectPoint,
                    ct);

            if (collectorPoint is null)
            {
                logger.LogWarning("Ponto de Coleta {CollectorPointId} não encontrado no preview de descarte.", collectorPointId);
                return ResultT<ResponseDiscardPreviewJson>.Failure(Error.NotFound("Ponto de Coleta não encontrado."));
            }

            if (collectorPoint.PersonStatus != PersonStatus.Active)
            {
                logger.LogWarning("Ponto de Coleta {CollectorPointId} inativo no preview de descarte.", collectorPointId);
                return ResultT<ResponseDiscardPreviewJson>.Failure(Error.Conflict("Ponto de Coleta não está ativo."));
            }

            var now = DateTime.UtcNow;
            var materials = (await dbContext.MaterialScoreRules
                .AsNoTracking()
                .Where(value => value.LegalPersonId == collectorPoint.Id
                    && value.ValidFrom <= now
                    && (value.ValidTo == null || value.ValidTo > now))
                .Select(value => value.Material)
                .Distinct()
                .Order()
                .ToListAsync(ct))
                .Select(value => (Ecocell.Shared.Enums.ElectronicMaterial)(int)value)
                .ToArray();

            return ResultT<ResponseDiscardPreviewJson>.Success(new ResponseDiscardPreviewJson
            {
                TradeName = collectorPoint.TradeName,
                FormattedAddress = FormatAddress(collectorPoint.Address),
                AcceptedMaterials = materials,
            });
        }

        private static string FormatAddress(Address? address)
        {
            if (address is null)
                return string.Empty;

            var complement = string.IsNullOrWhiteSpace(address.Complement)
                ? string.Empty
                : $", {address.Complement}";

            return $"{address.Street}, {address.Number}{complement} — "
                + $"{address.Neighborhood}, {address.City}/{address.State}";
        }
    }
}

public sealed class PreviewDiscardCollectorPointEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "api/v1/discards/preview",
            async ([FromQuery] string qrCode, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new PreviewDiscardCollectorPoint.Query(qrCode), ct);
                return result.ToProcessResult(StatusCodes.Status200OK);
            })
            .WithTags("Discard")
            .WithName("PreviewDiscardCollectorPoint")
            .WithSummary("Exibe o Ponto de Coleta e os materiais aceitos a partir do QR Code.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces<ResponseDiscardPreviewJson>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

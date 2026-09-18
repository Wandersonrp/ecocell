using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses.Discards;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Features.Discard;

public static class ListPendingDiscards
{
    public sealed record Query(Guid CollectorPointId)
        : IRequest<ResultT<ResponsePendingDiscardListJson>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(value => value.CollectorPointId)
            .NotEmpty()
            .WithMessage("O identificador do Ponto de Coleta é obrigatório.");
    }

    public sealed class Handler(
        AppDbContext dbContext,
        ILogger<Handler> logger,
        IValidator<Query> validator,
        ICollectorPointAccessGuard accessGuard)
        : IRequestHandler<Query, ResultT<ResponsePendingDiscardListJson>>
    {
        public async ValueTask<ResultT<ResponsePendingDiscardListJson>> Handle(Query request, CancellationToken ct)
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return ResultT<ResponsePendingDiscardListJson>.Failure(
                    Error.ErrorOnValidation(validation.Errors.Select(value => value.ErrorMessage).ToList()));
            }

            var access = await accessGuard.EnsureResponsibleActiveAsync(request.CollectorPointId, ct);
            if (access.IsFailure)
                return ResultT<ResponsePendingDiscardListJson>.Failure(access.Error);

            var discards = await dbContext.Discards
                .AsNoTracking()
                .Include(value => value.Depositor)
                .Include(value => value.Items)
                .Where(value => value.CollectorPointId == request.CollectorPointId
                    && value.Status == DiscardStatus.Pending)
                .OrderBy(value => value.CreatedAt)
                .ThenBy(value => value.Id)
                .ToListAsync(ct);

            var items = discards.Select(discard => new ResponsePendingDiscardJson
            {
                Id = discard.Id,
                CreatedAt = discard.CreatedAt,
                DepositorName = discard.Depositor.FullName,
                Items = discard.Items
                    .OrderBy(value => value.Material)
                    .Select(value => new ResponsePendingDiscardItemJson
                    {
                        Material = (Ecocell.Shared.Enums.ElectronicMaterial)(int)value.Material,
                        Quantity = value.Quantity,
                        ApproximateWeightKg = value.ApproximateWeightKg,
                    })
                    .ToArray(),
            }).ToArray();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation(
                    "Ponto de Coleta {CollectorPointId} possui {Count} descartes pendentes.",
                    request.CollectorPointId,
                    items.Length);

            return ResultT<ResponsePendingDiscardListJson>.Success(
                new ResponsePendingDiscardListJson { Items = items });
        }
    }
}

public sealed class ListPendingDiscardsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "api/v1/collector-points/{collectorPointId:guid}/discards/pending",
            async (Guid collectorPointId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListPendingDiscards.Query(collectorPointId), ct);
                return result.ToProcessResult(StatusCodes.Status200OK);
            })
            .WithTags("Discard")
            .WithName("ListPendingDiscards")
            .WithSummary("Lista os descartes pendentes de um Ponto de Coleta gerenciado.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces<ResponsePendingDiscardListJson>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

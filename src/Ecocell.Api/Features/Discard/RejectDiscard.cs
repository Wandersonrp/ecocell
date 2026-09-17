using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecocell.Api.Features.Discard;

public static class RejectDiscard
{
    public sealed record Command(Guid DiscardId) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(value => value.DiscardId)
                .NotEmpty()
                .WithMessage("O identificador do descarte é obrigatório.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly ICollectorPointAccessGuard _accessGuard;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Command> validator,
            ICollectorPointAccessGuard accessGuard)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _accessGuard = accessGuard;
        }

        public async ValueTask<Result> Handle(Command request, CancellationToken ct)
        {
            var validation = await _validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return Result.Failure(Error.ErrorOnValidation(
                    validation.Errors.Select(value => value.ErrorMessage).ToList()));
            }

            var discard = await _dbContext.Discards
                .SingleOrDefaultAsync(value => value.Id == request.DiscardId, ct);
            if (discard is null)
                return Result.Failure(Error.NotFound("Descarte não encontrado."));

            var access = await _accessGuard.EnsureResponsibleActiveAsync(
                discard.CollectorPointId,
                ct);
            if (access.IsFailure)
                return access;

            if (discard.Status != DiscardStatus.Pending)
                return Result.Failure(Error.Conflict("O descarte já foi processado."));

            discard.Reject();
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Conflito concorrente ao rejeitar o descarte {DiscardId}.",
                    discard.Id);
                return Result.Failure(Error.Conflict(
                    "O descarte foi alterado por outra operação."));
            }

            _logger.LogInformation(
                "Descarte {DiscardId} rejeitado pelo Ponto de Coleta {CollectorPointId}.",
                discard.Id,
                discard.CollectorPointId);
            return Result.Success();
        }
    }
}

public sealed class RejectDiscardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "api/v1/discards/{id:guid}/reject",
            async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RejectDiscard.Command(id), ct);
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("Discard")
            .WithName("RejectDiscard")
            .WithSummary("Rejeita um descarte pendente pertencente ao Ponto de Coleta.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

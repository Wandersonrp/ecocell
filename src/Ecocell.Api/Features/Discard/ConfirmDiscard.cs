using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests.Discards;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecocell.Api.Features.Discard;

public static class ConfirmDiscard
{
    public sealed record ItemCommand
    {
        public ElectronicMaterial Material { get; init; }
        public int Quantity { get; init; }
        public decimal ApproximateWeightKg { get; init; }
    }

    public sealed record Command : IRequest<Result>
    {
        public Guid DiscardId { get; init; }
        public IReadOnlyList<ItemCommand> Items { get; init; } = [];
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(value => value.DiscardId)
                .NotEmpty()
                .WithMessage("O identificador do descarte é obrigatório.");

            RuleFor(value => value.Items)
                .NotEmpty()
                .WithMessage("Informe ao menos um item.");

            RuleFor(value => value.Items)
                .Must(items => items is not null
                    && items.All(item => item is not null)
                    && items.Select(item => item.Material).Distinct().Count() == items.Count)
                .WithMessage("Cada material pode aparecer somente uma vez.");

            RuleForEach(value => value.Items)
                .NotNull()
                .WithMessage("O item do descarte é obrigatório.");

            RuleForEach(value => value.Items).ChildRules(item =>
            {
                item.RuleFor(value => value.Material)
                    .IsInEnum()
                    .Must(value => Convert.ToInt32(value) > 0)
                    .WithMessage("O material é inválido.");
                item.RuleFor(value => value.Quantity)
                    .GreaterThan(0)
                    .WithMessage("A quantidade deve ser maior que zero.");
                item.RuleFor(value => value.ApproximateWeightKg)
                    .GreaterThan(0)
                    .WithMessage("O peso aproximado deve ser maior que zero.")
                    .PrecisionScale(10, 3, false)
                    .WithMessage("O peso aproximado deve ter até 10 dígitos totais e 3 casas decimais.");
            });
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

        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Result.Failure(Error.ErrorOnValidation(
                    validation.Errors.Select(value => value.ErrorMessage).ToList()));
            }

            var discard = await _dbContext.Discards
                .Include(value => value.Items)
                .SingleOrDefaultAsync(value => value.Id == request.DiscardId, cancellationToken);

            if (discard is null)
                return Result.Failure(Error.NotFound("Descarte não encontrado."));

            var access = await _accessGuard.EnsureResponsibleActiveAsync(
                discard.CollectorPointId,
                cancellationToken);
            if (access.IsFailure)
                return access;

            if (discard.Status != DiscardStatus.Pending)
                return Result.Failure(Error.Conflict("O descarte já foi processado."));

            var requestedItems = request.Items.ToArray();
            var materials = requestedItems.Select(value => value.Material).ToArray();
            var rules = await _dbContext.MaterialScoreRules
                .AsNoTracking()
                .Where(value => value.LegalPersonId == discard.CollectorPointId
                    && materials.Contains(value.Material)
                    && value.ValidFrom <= discard.CreatedAt
                    && (value.ValidTo == null || value.ValidTo > discard.CreatedAt))
                .ToListAsync(cancellationToken);
            var rulesByMaterial = rules.ToDictionary(value => value.Material);
            var unsupported = materials
                .Where(value => !rulesByMaterial.ContainsKey(value))
                .Distinct()
                .Order()
                .ToArray();

            if (unsupported.Length > 0)
            {
                return Result.Failure(Error.Conflict(
                    $"Materiais sem regra vigente na abertura: {string.Join(", ", unsupported)}."));
            }

            var finalItems = requestedItems
                .Select(value => new DiscardItem(
                    value.Material,
                    value.Quantity,
                    value.ApproximateWeightKg,
                    rulesByMaterial[value.Material].Id))
                .ToArray();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _dbContext.DiscardItems.RemoveRange(discard.Items);
                await _dbContext.SaveChangesAsync(cancellationToken);

                discard.Confirm(finalItems);
                _dbContext.CreditScoreRequests.Add(new CreditScoreRequest(discard.Id));
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    exception,
                    "Conflito concorrente ao confirmar o descarte {DiscardId}.",
                    discard.Id);
                return Result.Failure(Error.Conflict(
                    "O descarte foi alterado por outra operação."));
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "Descarte {DiscardId} confirmado pelo Ponto de Coleta {CollectorPointId}.",
                    discard.Id,
                    discard.CollectorPointId);
            return Result.Success();
        }
    }
}

public sealed class ConfirmDiscardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "api/v1/discards/{id:guid}/confirm",
            async (
                Guid id,
                [FromBody] RequestConfirmDiscardJson request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new ConfirmDiscard.Command
                {
                    DiscardId = id,
                    Items = (request.Items ?? [])
                        .Select(item => item is null
                            ? null!
                            : new ConfirmDiscard.ItemCommand
                            {
                                Material = (ElectronicMaterial)(int)item.Material,
                                Quantity = item.Quantity,
                                ApproximateWeightKg = item.ApproximateWeightKg,
                            })
                        .ToArray(),
                };

                var result = await sender.Send(command, ct);
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("Discard")
            .WithName("ConfirmDiscard")
            .WithSummary("Confirma um descarte pendente com a composição final validada pelo Ponto de Coleta.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

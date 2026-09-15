using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests.CollectorPoints;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecocell.Api.Features.CollectorPoint;

public static class SetMaterialScoreRules
{
    private const int MaximumRules = 7;

    public sealed record RuleInput(
        ElectronicMaterial Material,
        decimal Points,
        MaterialScoreUnit Unit);

    public sealed record Command(
        Guid CollectorPointId,
        IReadOnlyList<RuleInput>? Rules) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CollectorPointId)
                .NotEmpty()
                .WithMessage("O identificador do ponto de coleta é obrigatório.");

            RuleFor(x => x.Rules)
                .NotNull()
                .Must(rules => rules is { Count: >= 1 and <= MaximumRules })
                .WithMessage("Informe de 1 a 7 regras de pontuação.");

            When(x => x.Rules is not null, () =>
            {
                RuleFor(x => x.Rules!)
                    .Must(rules => rules.DistinctBy(rule => rule.Material).Count() == rules.Count)
                    .WithMessage("Cada material pode aparecer somente uma vez.");

                RuleForEach(x => x.Rules!).ChildRules(rule =>
                {
                    rule.RuleFor(x => x.Material)
                        .IsInEnum()
                        .WithMessage("O material informado é inválido.");

                    rule.RuleFor(x => x.Points)
                        .GreaterThan(0m)
                        .WithMessage("A pontuação deve ser maior que zero.")
                        .PrecisionScale(10, 2, false)
                        .WithMessage("A pontuação deve ter até 10 dígitos totais e 2 casas decimais.");

                    rule.RuleFor(x => x.Unit)
                        .IsInEnum()
                        .WithMessage("A unidade de pontuação informada é inválida.");
                });
            });
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private const string OpenRuleIndexName = "IX_MaterialScoreRules_LegalPersonId_Material";
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly ICollectorPointAccessGuard _accessGuard;
        private readonly TimeProvider _timeProvider;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Command> validator,
            ICollectorPointAccessGuard accessGuard,
            TimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _accessGuard = accessGuard;
            _timeProvider = timeProvider;
        }

        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Result.Failure(Error.ErrorOnValidation(
                    validation.Errors.Select(error => error.ErrorMessage).ToList()));
            }

            var access = await _accessGuard.EnsureResponsibleActiveAsync(
                request.CollectorPointId,
                cancellationToken);
            if (access.IsFailure)
                return access;

            var currentRules = await _dbContext.MaterialScoreRules
                .Where(rule => rule.LegalPersonId == request.CollectorPointId
                               && rule.ValidTo == null)
                .ToListAsync(cancellationToken);
            var currentByMaterial = currentRules.ToDictionary(rule => rule.Material);
            var requestedByMaterial = request.Rules!.ToDictionary(rule => rule.Material);
            var toClose = new List<MaterialScoreRule>();
            var toCreate = new List<RuleInput>();
            var unchangedCount = 0;
            var replacedCount = 0;
            var closedCount = 0;
            var createdCount = 0;

            foreach (var current in currentRules)
            {
                if (!requestedByMaterial.TryGetValue(current.Material, out var requested))
                {
                    toClose.Add(current);
                    closedCount++;
                    continue;
                }

                if (current.Points == requested.Points && current.Unit == requested.Unit)
                {
                    unchangedCount++;
                    continue;
                }

                toClose.Add(current);
                toCreate.Add(requested);
                replacedCount++;
            }

            foreach (var requested in requestedByMaterial.Values)
            {
                if (!currentByMaterial.ContainsKey(requested.Material))
                {
                    toCreate.Add(requested);
                    createdCount++;
                }
            }

            if (toClose.Count == 0 && toCreate.Count == 0)
            {
                _logger.LogInformation(
                    "Tabela de pontuação do PC {CollectorPointId} já estava atual. Inalteradas: {UnchangedCount}.",
                    request.CollectorPointId,
                    unchangedCount);
                return Result.Success();
            }

            var effectiveAt = _timeProvider.GetUtcNow().UtcDateTime;
            if (toClose.Count > 0)
            {
                var latestValidFrom = toClose.Max(rule => rule.ValidFrom);
                if (effectiveAt < latestValidFrom)
                {
                    _logger.LogWarning(
                        "Relógio regrediu ao versionar regras do PC {CollectorPointId}. Agora: {Now}; última vigência: {LatestValidFrom}.",
                        request.CollectorPointId,
                        effectiveAt,
                        latestValidFrom);
                    return Result.Failure(Error.Conflict(
                        "Não foi possível versionar as regras devido à ordem temporal."));
                }

                if (effectiveAt == latestValidFrom)
                    effectiveAt = latestValidFrom.AddTicks(10);

            }

            foreach (var current in toClose)
                current.Close(effectiveAt);

            foreach (var requested in toCreate)
            {
                _dbContext.MaterialScoreRules.Add(new MaterialScoreRule(
                    request.CollectorPointId,
                    requested.Material,
                    requested.Points,
                    requested.Unit,
                    effectiveAt));
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsOpenRuleConflict(exception))
            {
                _logger.LogWarning(
                    exception,
                    "Conflito concorrente ao versionar regras do PC {CollectorPointId}.",
                    request.CollectorPointId);
                return Result.Failure(Error.Conflict(
                    "A tabela foi alterada por outra operação. Recarregue e tente novamente."));
            }

            _logger.LogInformation(
                "Tabela do PC {CollectorPointId} atualizada. Novas: {CreatedCount}; substituídas: {ReplacedCount}; encerradas: {ClosedCount}; inalteradas: {UnchangedCount}.",
                request.CollectorPointId,
                createdCount,
                replacedCount,
                closedCount,
                unchangedCount);

            return Result.Success();
        }

        private static bool IsOpenRuleConflict(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == OpenRuleIndexName;
    }
}

public sealed class SetMaterialScoreRulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
            "api/collector-points/{collectorPointId:guid}/score-rules",
            async (
                Guid collectorPointId,
                [FromBody] RequestSetMaterialScoreRulesJson request,
                ISender sender) =>
            {
                var rules = request.Rules?.Select(rule =>
                        new SetMaterialScoreRules.RuleInput(
                            (ElectronicMaterial)(int)rule.Material,
                            rule.Points,
                            (MaterialScoreUnit)(int)rule.Unit))
                    .ToList();

                var result = await sender.Send(
                    new SetMaterialScoreRules.Command(collectorPointId, rules));
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("CollectorPoint")
            .WithName("SetMaterialScoreRules")
            .WithSummary("Substitui a tabela vigente de pontuação de um Ponto de Coleta.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

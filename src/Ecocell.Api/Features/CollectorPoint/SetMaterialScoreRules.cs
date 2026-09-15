using Ecocell.Api.Enums;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;

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
}

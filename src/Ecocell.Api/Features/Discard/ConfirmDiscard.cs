using Ecocell.Api.Enums;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;

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
                    && items.Select(item => item!.Material).Distinct().Count() == items.Count)
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
}

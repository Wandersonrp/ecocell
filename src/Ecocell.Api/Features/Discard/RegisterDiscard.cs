using Ecocell.Api.Enums;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;

namespace Ecocell.Api.Features.Discard;

public static class RegisterDiscard
{
    public sealed record ItemCommand
    {
        public ElectronicMaterial Material { get; init; }
        public int Quantity { get; set; }
        public decimal ApproximateWeightKg { get; set; }
    }

    public sealed record Command : IRequest<ResultT<ResponseRegisterDiscardJson>>
    {
        public string QrCode { get; init; } = string.Empty;
        public IReadOnlyList<ItemCommand> Items { get; init; } = [];
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(value => value.QrCode)
                .NotEmpty()
                .Must(value => TryGetCollectorPointId(value, out _))
                .WithMessage("O QR Code do Ponto de Coleta é inválido.");

            RuleFor(value => value.Items)
                .NotEmpty()
                .WithMessage("Informe ao menos um item.");

            RuleFor(value => value.Items)
                .Must(items => items.Select(item => item.Material).Distinct().Count() == items.Count)
                .WithMessage("Cada material pode aparecer somente uma vez.");

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
                    .WithMessage("O peso aproximado deve ser maior que zero.");
            });
        }
    }

    internal static bool TryGetCollectorPointId(string qrCode, out Guid id)
    {
        id = Guid.Empty;

        if (!Uri.TryCreate(qrCode, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals("ecocell", StringComparison.OrdinalIgnoreCase)
            || !uri.Host.Equals("pc", StringComparison.OrdinalIgnoreCase)
            || uri.Port != -1
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0)
        {
            return false;
        }

        var path = uri.AbsolutePath;
        return path.Length == 37
            && path[0] == '/'
            && Guid.TryParseExact(path[1..], "D", out id);
    }
}

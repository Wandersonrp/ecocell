using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses.Discards;

public sealed record ResponsePendingDiscardItemJson
{
    public ElectronicMaterial Material { get; init; }
    public int Quantity { get; init; }
    public decimal ApproximateWeightKg { get; init; }
}

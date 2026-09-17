using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses.Discards;

public sealed record ResponseDiscardPreviewJson
{
    public string TradeName { get; init; } = string.Empty;
    public string FormattedAddress { get; init; } = string.Empty;
    public IReadOnlyList<ElectronicMaterial> AcceptedMaterials { get; init; } = [];
}

using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.Discards;

/// <summary>Solicita a abertura de um descarte em um Ponto de Coleta.</summary>
public sealed record RequestRegisterDiscardJson
{
    public string QrCode { get; init; } = string.Empty;
    public IReadOnlyList<RequestRegisterDiscardItemJson> Items { get; init; } = [];
}

/// <summary>Informa um material declarado no descarte.</summary>
public sealed record RequestRegisterDiscardItemJson
{
    public ElectronicMaterial Material { get; init; }
    public int Quantity { get; init; }
    public decimal ApproximateWeightKg { get; init; }
}

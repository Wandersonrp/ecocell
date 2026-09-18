using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.Discards;

/// <summary>Solicita a confirmação de um descarte com a composição final recebida.</summary>
public sealed record RequestConfirmDiscardJson
{
    public IReadOnlyList<RequestConfirmDiscardItemJson> Items { get; init; } = [];
}

/// <summary>Informa um material validado pelo Ponto de Coleta.</summary>
public sealed record RequestConfirmDiscardItemJson
{
    public ElectronicMaterial Material { get; init; }
    public int Quantity { get; init; }
    public decimal ApproximateWeightKg { get; init; }
}

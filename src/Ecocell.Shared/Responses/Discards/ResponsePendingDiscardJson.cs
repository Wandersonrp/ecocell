namespace Ecocell.Shared.Responses.Discards;

public sealed record ResponsePendingDiscardJson
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string DepositorName { get; init; } = string.Empty;
    public IReadOnlyList<ResponsePendingDiscardItemJson> Items { get; init; } = [];
}

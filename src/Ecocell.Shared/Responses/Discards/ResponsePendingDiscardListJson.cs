namespace Ecocell.Shared.Responses.Discards;

public sealed record ResponsePendingDiscardListJson
{
    public IReadOnlyList<ResponsePendingDiscardJson> Items { get; init; } = [];
}

namespace Ecocell.Shared.Responses.Ranking;

public sealed record ResponseRankingJson
{
    public IReadOnlyList<ResponseRankingItemJson> Items { get; init; } = [];
    public ResponseRankingItemJson? CurrentUser { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore { get; init; }
}

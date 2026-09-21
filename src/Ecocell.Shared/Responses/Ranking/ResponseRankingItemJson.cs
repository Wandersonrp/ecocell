namespace Ecocell.Shared.Responses.Ranking;

public sealed record ResponseRankingItemJson
{
    public string ReducedName { get; init; } = string.Empty;
    public long Position { get; init; }
    public decimal Points { get; init; }
    public bool IsCurrentUser { get; init; }
}

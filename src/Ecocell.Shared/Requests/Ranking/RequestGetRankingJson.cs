using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.Ranking;

public sealed record RequestGetRankingJson
{
    public RankingScope? Scope { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

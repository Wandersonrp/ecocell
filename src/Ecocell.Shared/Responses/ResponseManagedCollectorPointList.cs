namespace Ecocell.Shared.Responses;

public sealed record ResponseManagedCollectorPointList
{
    public IReadOnlyList<ResponseManagedCollectorPoint> Items { get; init; } = [];
}

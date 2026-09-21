namespace Ecocell.Shared.Responses;

public sealed record ResponsePartnerList
{
    public List<ResponsePartnerItem> Items { get; init; } = new();
    public Guid? NextCursor { get; init; }
    public bool HasMore { get; init; }
}
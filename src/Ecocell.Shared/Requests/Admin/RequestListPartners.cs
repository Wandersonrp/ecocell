using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.Admin;

public record RequestListPartners
{
    public PersonStatus? Status { get; init; }
    public Journey? Journey { get; init; }
    public Guid? Cursor { get; init; }
    public int? PageSize { get; init; }
}
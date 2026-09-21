namespace Ecocell.Shared.Requests.Admin;

public record RequestRejectPartner
{
    public string Reason { get; init; } = string.Empty;
}
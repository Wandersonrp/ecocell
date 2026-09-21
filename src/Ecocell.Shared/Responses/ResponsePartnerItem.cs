using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses;

public sealed record ResponsePartnerItem
{
    public Guid ExternalId { get; init; }
    public string LegalName { get; init; } = string.Empty;
    public string TradeName { get; init; } = string.Empty;
    public string Cnpj { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public PersonStatus Status { get; init; }
    public Journey Journey { get; init; }
    public DateTime CreatedAt { get; init; }
}
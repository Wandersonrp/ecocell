using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses;

public sealed record ResponseManagedCollectorPoint
{
    public Guid Id { get; init; }
    public string TradeName { get; init; } = string.Empty;
    public string LegalName { get; init; } = string.Empty;
    public string Cnpj { get; init; } = string.Empty;
    public PersonStatus Status { get; init; }
}

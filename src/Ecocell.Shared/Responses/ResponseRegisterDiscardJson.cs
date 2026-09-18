using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses;

/// <summary>Representa um descarte aberto com sucesso.</summary>
public sealed record ResponseRegisterDiscardJson
{
    public Guid Id { get; init; }
    public DiscardStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

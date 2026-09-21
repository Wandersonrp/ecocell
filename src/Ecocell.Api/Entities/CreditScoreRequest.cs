namespace Ecocell.Api.Entities;

/// <summary>Solicitação durável de processamento do crédito de um descarte confirmado.</summary>
public sealed class CreditScoreRequest : BaseEntity
{
    private CreditScoreRequest()
    {
    }

    public CreditScoreRequest(Guid discardId)
    {
        if (discardId == Guid.Empty)
            throw new ArgumentException("O identificador do descarte é obrigatório.", nameof(discardId));

        DiscardId = discardId;
    }

    public Guid DiscardId { get; private set; }
    public Discard Discard { get; private set; } = null!;
    public DateTime? DispatchedAt { get; private set; }

    public void MarkAsDispatched(DateTime dispatchedAt)
    {
        if (DispatchedAt is not null)
            throw new InvalidOperationException("A solicitação de crédito já foi processada.");
        if (dispatchedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A data deve estar em UTC.", nameof(dispatchedAt));

        DispatchedAt = dispatchedAt;
        MarkAsUpdated();
    }
}

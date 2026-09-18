namespace Ecocell.Api.Entities;

/// <summary>Crédito imutável concedido ao depositante por um descarte confirmado.</summary>
public sealed class DepositorScoreTransaction : BaseEntity
{
    private DepositorScoreTransaction()
    {
    }

    public DepositorScoreTransaction(Guid discardId, Guid depositorId, decimal points)
    {
        if (discardId == Guid.Empty)
            throw new ArgumentException("O identificador do descarte é obrigatório.", nameof(discardId));
        if (depositorId == Guid.Empty)
            throw new ArgumentException("O identificador do depositante é obrigatório.", nameof(depositorId));
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "A pontuação deve ser maior que zero.");

        DiscardId = discardId;
        DepositorId = depositorId;
        Points = points;
    }

    public Guid DiscardId { get; private set; }
    public Discard Discard { get; private set; } = null!;
    public Guid DepositorId { get; private set; }
    public NaturalPerson Depositor { get; private set; } = null!;
    public decimal Points { get; private set; }
}

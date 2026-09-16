namespace Ecocell.Api.Entities;

/// <summary>Saldo global materializado de pontos de um depositante.</summary>
public sealed class DepositorTotalScore : BaseEntity
{
    private DepositorTotalScore()
    {
    }

    public DepositorTotalScore(Guid depositorId, decimal initialPoints)
    {
        if (depositorId == Guid.Empty)
            throw new ArgumentException("O identificador do depositante é obrigatório.", nameof(depositorId));
        if (initialPoints <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(initialPoints),
                "A pontuação inicial deve ser maior que zero.");

        DepositorId = depositorId;
        TotalPoints = initialPoints;
    }

    public Guid DepositorId { get; private set; }
    public NaturalPerson Depositor { get; private set; } = null!;
    public decimal TotalPoints { get; private set; }

    public void Credit(decimal points)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "A pontuação deve ser maior que zero.");

        TotalPoints += points;
        MarkAsUpdated();
    }
}

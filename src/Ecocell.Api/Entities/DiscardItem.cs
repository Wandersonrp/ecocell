using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public sealed class DiscardItem : BaseEntity
{
    private DiscardItem() { }

    public DiscardItem(
        ElectronicMaterial material,
        int quantity,
        decimal approximateWeightKg,
        Guid materialScoreRuleId)
    {
        if (Convert.ToInt32(material) <= 0)
            throw new ArgumentOutOfRangeException(nameof(material));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(approximateWeightKg);
        if (materialScoreRuleId == Guid.Empty)
            throw new ArgumentException("A regra de pontuação é obrigatória.", nameof(materialScoreRuleId));

        Material = material;
        Quantity = quantity;
        ApproximateWeightKg = approximateWeightKg;
        MaterialScoreRuleId = materialScoreRuleId;
    }

    public Guid DiscardId { get; private set; } // NOSONAR: EF Core materializes this foreign key through the private setter.
    public Discard Discard { get; private set; } = null!;
    public ElectronicMaterial Material { get; private set; }
    public int Quantity { get; private set; }
    public decimal ApproximateWeightKg { get; private set; }
    public Guid MaterialScoreRuleId { get; private set; }
    public MaterialScoreRule MaterialScoreRule { get; private set; } = null!;
}

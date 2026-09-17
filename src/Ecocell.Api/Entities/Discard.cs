using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public sealed class Discard : BaseEntity
{
    private readonly List<DiscardItem> _items = [];

    private Discard() { }

    public Discard(
        Guid depositorId,
        Guid collectorPointId,
        IEnumerable<DiscardItem> items)
    {
        if (depositorId == Guid.Empty)
            throw new ArgumentException("O depositante é obrigatório.", nameof(depositorId));
        if (collectorPointId == Guid.Empty)
            throw new ArgumentException("O Ponto de Coleta é obrigatório.", nameof(collectorPointId));

        var itemList = ValidateItems(items);

        DepositorId = depositorId;
        CollectorPointId = collectorPointId;
        Status = DiscardStatus.Pending;
        _items.AddRange(itemList);
    }

    public Guid DepositorId { get; private set; }
    public NaturalPerson Depositor { get; private set; } = null!;
    public Guid CollectorPointId { get; private set; }
    public LegalPerson CollectorPoint { get; private set; } = null!;
    public DiscardStatus Status { get; private set; }
    public IReadOnlyCollection<DiscardItem> Items => _items;

    public void Confirm(IEnumerable<DiscardItem> finalItems)
    {
        EnsurePending();
        var itemList = ValidateItems(finalItems);

        _items.Clear();
        _items.AddRange(itemList);
        Status = DiscardStatus.Confirmed;
        MarkAsUpdated();
    }

    public void Reject()
    {
        EnsurePending();
        Status = DiscardStatus.Rejected;
        MarkAsUpdated();
    }

    private void EnsurePending()
    {
        if (Status != DiscardStatus.Pending)
            throw new InvalidOperationException("Somente descartes pendentes podem ser alterados.");
    }

    private static List<DiscardItem> ValidateItems(IEnumerable<DiscardItem> items)
    {
        var itemList = items?.ToList()
            ?? throw new ArgumentNullException(nameof(items));

        if (itemList.Count == 0)
            throw new ArgumentException("O descarte deve possuir ao menos um item.", nameof(items));

        if (itemList.Select(item => item.Material).Distinct().Count() != itemList.Count)
            throw new ArgumentException("Cada material pode aparecer somente uma vez.", nameof(items));

        return itemList;
    }
}

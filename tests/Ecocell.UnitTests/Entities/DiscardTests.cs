using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class DiscardTests
{
    private static ElectronicMaterial Material =>
        Enum.GetValues<ElectronicMaterial>()
            .First(value => Convert.ToInt32(value) > 0);

    [Fact]
    public void Constructor_ShouldCreatePendingDiscard_WhenItemsAreValid()
    {
        var item = new DiscardItem(Material, 2, 0.450m, Guid.NewGuid());

        var discard = new Discard(Guid.NewGuid(), Guid.NewGuid(), [item]);

        discard.Status.ShouldBe(DiscardStatus.Pending);
        discard.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenItemsAreEmpty()
    {
        Should.Throw<ArgumentException>(
            () => new Discard(Guid.NewGuid(), Guid.NewGuid(), []));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMaterialIsDuplicated()
    {
        var first = new DiscardItem(Material, 1, 0.200m, Guid.NewGuid());
        var second = new DiscardItem(Material, 2, 0.400m, Guid.NewGuid());

        Should.Throw<ArgumentException>(
            () => new Discard(Guid.NewGuid(), Guid.NewGuid(), [first, second]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrow_WhenQuantityIsNotPositive(int quantity)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new DiscardItem(Material, quantity, 0.100m, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenWeightIsNotPositive()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new DiscardItem(Material, 1, 0m, Guid.NewGuid()));
    }

    [Fact]
    public void Confirm_ShouldReplaceItemsAndSetConfirmed_WhenDiscardIsPending()
    {
        var original = new DiscardItem(Material, 1, 0.100m, Guid.NewGuid());
        var replacementMaterial = Enum.GetValues<ElectronicMaterial>()
            .First(value => value != Material && Convert.ToInt32(value) > 0);
        var replacement = new DiscardItem(replacementMaterial, 3, 0.750m, Guid.NewGuid());
        var discard = new Discard(Guid.NewGuid(), Guid.NewGuid(), [original]);

        discard.Confirm([replacement]);

        discard.Status.ShouldBe(DiscardStatus.Confirmed);
        discard.Items.ShouldHaveSingleItem();
        discard.Items.Single().ShouldBeSameAs(replacement);
        discard.UpdatedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(DiscardStatus.Confirmed)]
    [InlineData(DiscardStatus.Rejected)]
    public void Confirm_ShouldThrow_WhenDiscardIsNotPending(DiscardStatus terminalStatus)
    {
        var discard = PendingDiscard();
        MoveTo(discard, terminalStatus);

        Should.Throw<InvalidOperationException>(() =>
            discard.Confirm([new DiscardItem(Material, 1, 0.200m, Guid.NewGuid())]));
    }

    [Fact]
    public void Confirm_ShouldThrow_WhenFinalItemsAreEmpty()
    {
        var discard = PendingDiscard();

        Should.Throw<ArgumentException>(() => discard.Confirm([]));
    }

    [Fact]
    public void Confirm_ShouldThrow_WhenFinalMaterialsAreDuplicated()
    {
        var discard = PendingDiscard();
        var first = new DiscardItem(Material, 1, 0.200m, Guid.NewGuid());
        var second = new DiscardItem(Material, 2, 0.400m, Guid.NewGuid());

        Should.Throw<ArgumentException>(() => discard.Confirm([first, second]));
    }

    [Fact]
    public void Reject_ShouldSetRejected_WhenDiscardIsPending()
    {
        var discard = PendingDiscard();

        discard.Reject();

        discard.Status.ShouldBe(DiscardStatus.Rejected);
        discard.UpdatedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(DiscardStatus.Confirmed)]
    [InlineData(DiscardStatus.Rejected)]
    public void Reject_ShouldThrow_WhenDiscardIsNotPending(DiscardStatus terminalStatus)
    {
        var discard = PendingDiscard();
        MoveTo(discard, terminalStatus);

        Should.Throw<InvalidOperationException>(discard.Reject);
    }

    private static Discard PendingDiscard() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DiscardItem(Material, 1, 0.100m, Guid.NewGuid())]);

    private static void MoveTo(Discard discard, DiscardStatus status)
    {
        if (status == DiscardStatus.Confirmed)
            discard.Confirm([new DiscardItem(Material, 1, 0.100m, Guid.NewGuid())]);
        else
            discard.Reject();
    }
}

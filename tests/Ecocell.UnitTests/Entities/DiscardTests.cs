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
}

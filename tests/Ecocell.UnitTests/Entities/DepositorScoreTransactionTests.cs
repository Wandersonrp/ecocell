using Ecocell.Api.Entities;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class DepositorScoreTransactionTests
{
    [Fact]
    public void Constructor_ShouldStoreValues_WhenValuesAreValid()
    {
        var discardId = Guid.NewGuid();
        var depositorId = Guid.NewGuid();

        var transaction = new DepositorScoreTransaction(discardId, depositorId, 12.34567m);

        transaction.DiscardId.ShouldBe(discardId);
        transaction.DepositorId.ShouldBe(depositorId);
        transaction.Points.ShouldBe(12.34567m);
    }

    [Theory]
    [InlineData("discard")]
    [InlineData("depositor")]
    public void Constructor_ShouldThrow_WhenIdentifierIsEmpty(string identifier)
    {
        var discardId = identifier == "discard" ? Guid.Empty : Guid.NewGuid();
        var depositorId = identifier == "depositor" ? Guid.Empty : Guid.NewGuid();

        Should.Throw<ArgumentException>(() =>
            new DepositorScoreTransaction(discardId, depositorId, 1m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00001")]
    public void Constructor_ShouldThrow_WhenPointsAreNotPositive(string rawPoints)
    {
        var points = decimal.Parse(rawPoints, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DepositorScoreTransaction(Guid.NewGuid(), Guid.NewGuid(), points));
    }
}

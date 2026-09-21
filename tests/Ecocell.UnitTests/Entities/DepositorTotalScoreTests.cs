using Ecocell.Api.Entities;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class DepositorTotalScoreTests
{
    [Fact]
    public void Constructor_ShouldStoreInitialPoints_WhenValuesAreValid()
    {
        var depositorId = Guid.NewGuid();

        var total = new DepositorTotalScore(depositorId, 10.12345m);

        total.DepositorId.ShouldBe(depositorId);
        total.TotalPoints.ShouldBe(10.12345m);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDepositorIdIsEmpty()
    {
        Should.Throw<ArgumentException>(() =>
            new DepositorTotalScore(Guid.Empty, 1m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00001")]
    public void Constructor_ShouldThrow_WhenInitialPointsAreNotPositive(string rawPoints)
    {
        var points = decimal.Parse(rawPoints, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DepositorTotalScore(Guid.NewGuid(), points));
    }

    [Fact]
    public void Credit_ShouldAccumulatePoints_WhenPointsArePositive()
    {
        var total = new DepositorTotalScore(Guid.NewGuid(), 10.12345m);

        total.Credit(2.00001m);

        total.TotalPoints.ShouldBe(12.12346m);
        total.UpdatedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00001")]
    public void Credit_ShouldThrow_WhenPointsAreNotPositive(string rawPoints)
    {
        var total = new DepositorTotalScore(Guid.NewGuid(), 1m);
        var points = decimal.Parse(rawPoints, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() => total.Credit(points));
    }
}

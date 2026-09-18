using System.Globalization;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class MaterialScoreRuleTests
{
    private static readonly DateTime ValidFrom =
        new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ShouldCreateOpenRule_WhenDataIsValid()
    {
        // Arrange
        var legalPersonId = Guid.NewGuid();

        // Act
        var rule = new MaterialScoreRule(
            legalPersonId,
            ElectronicMaterial.Battery,
            12.50m,
            MaterialScoreUnit.PerUnit,
            ValidFrom);

        // Assert
        rule.LegalPersonId.ShouldBe(legalPersonId);
        rule.Material.ShouldBe(ElectronicMaterial.Battery);
        rule.Points.ShouldBe(12.50m);
        rule.Unit.ShouldBe(MaterialScoreUnit.PerUnit);
        rule.ValidFrom.ShouldBe(ValidFrom);
        rule.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLegalPersonIdIsEmpty()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new MaterialScoreRule(
            Guid.Empty,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            ValidFrom));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Constructor_ShouldThrow_WhenMaterialIsInvalid(int value)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            (ElectronicMaterial)value,
            10m,
            MaterialScoreUnit.PerUnit,
            ValidFrom));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Constructor_ShouldThrow_WhenUnitIsInvalid(int value)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            10m,
            (MaterialScoreUnit)value,
            ValidFrom));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    public void Constructor_ShouldThrow_WhenPointsAreNotPositive(string value)
    {
        // Arrange
        var points = decimal.Parse(value, CultureInfo.InvariantCulture);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            points,
            MaterialScoreUnit.PerUnit,
            ValidFrom));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_ShouldThrow_WhenValidFromIsNotUtc(DateTimeKind kind)
    {
        // Arrange
        var invalidDate = DateTime.SpecifyKind(new DateTime(2026, 9, 14, 12, 0, 0), kind);

        // Act & Assert
        Should.Throw<ArgumentException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            invalidDate));
    }

    [Fact]
    public void Close_ShouldSetValidToAndUpdatedAt_WhenDateIsValid()
    {
        // Arrange
        var rule = CreateRule();
        var closedAt = ValidFrom.AddHours(1);
        var beforeClose = DateTime.UtcNow;

        // Act
        rule.Close(closedAt);

        // Assert
        rule.ValidTo.ShouldBe(closedAt);
        rule.UpdatedAt.ShouldNotBeNull();
        rule.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeClose);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Close_ShouldThrow_WhenDateIsNotAfterValidFrom(int seconds)
    {
        // Arrange
        var rule = CreateRule();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            rule.Close(ValidFrom.AddSeconds(seconds)));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Close_ShouldThrow_WhenDateIsNotUtc(DateTimeKind kind)
    {
        // Arrange
        var rule = CreateRule();
        var invalidDate = DateTime.SpecifyKind(ValidFrom.AddHours(1), kind);

        // Act & Assert
        Should.Throw<ArgumentException>(() => rule.Close(invalidDate));
    }

    [Fact]
    public void Close_ShouldThrow_WhenRuleIsAlreadyClosed()
    {
        // Arrange
        var rule = CreateRule();
        rule.Close(ValidFrom.AddHours(1));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            rule.Close(ValidFrom.AddHours(2)));
    }

    private static MaterialScoreRule CreateRule() =>
        new(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            ValidFrom);
}

using System.Globalization;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Discard;
using Shouldly;

namespace Ecocell.UnitTests.Features.Discard;

public class ConfirmDiscardTests : TestBase
{
    private static ElectronicMaterial Material =>
        Enum.GetValues<ElectronicMaterial>()
            .First(value => Convert.ToInt32(value) > 0);

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand(), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenDiscardIdIsEmpty()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { DiscardId = Guid.Empty }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
    {
        var item = ValidItem() with { Quantity = quantity };
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    [InlineData("0.0001")]
    [InlineData("10000000.000")]
    public async Task Validate_ShouldFail_WhenWeightIsInvalid(string rawWeight)
    {
        var weight = decimal.Parse(rawWeight, CultureInfo.InvariantCulture);
        var item = ValidItem() with { ApproximateWeightKg = weight };
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreNull()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = null! }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreEmpty()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemIsNull()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [null!] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
    {
        var item = ValidItem() with { Material = (ElectronicMaterial)0 };
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
    {
        var item = ValidItem();
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item, item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    private static ConfirmDiscard.ItemCommand ValidItem() =>
        new()
        {
            Material = Material,
            Quantity = 1,
            ApproximateWeightKg = 0.250m,
        };

    private static ConfirmDiscard.Command ValidCommand(Guid? discardId = null) =>
        new()
        {
            DiscardId = discardId ?? Guid.NewGuid(),
            Items = [ValidItem()],
        };
}

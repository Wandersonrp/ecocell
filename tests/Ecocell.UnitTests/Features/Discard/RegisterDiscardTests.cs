using Ecocell.Api.Enums;
using Ecocell.Api.Features.Discard;
using Shouldly;

namespace Ecocell.UnitTests.Features.Discard;

public class RegisterDiscardTests
{
    private static ElectronicMaterial Material =>
        Enum.GetValues<ElectronicMaterial>().First();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand(), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-qr")]
    [InlineData("https://pc/018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
    [InlineData("ecocell://collector/018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
    [InlineData("ecocell://pc/not-a-guid")]
    public async Task Validate_ShouldFail_WhenQrCodeIsInvalid(string qrCode)
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { QrCode = qrCode }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreEmpty()
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
    {
        var item = ValidCommand().Items[0];
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item, item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
    {
        var command = ValidCommand();
        command.Items[0].Quantity = quantity;

        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(command, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    public async Task Validate_ShouldFail_WhenWeightIsNotPositive(string weight)
    {
        var command = ValidCommand();
        command.Items[0].ApproximateWeightKg = decimal.Parse(
            weight,
            System.Globalization.CultureInfo.InvariantCulture);

        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(command, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
    {
        var command = ValidCommand();
        var invalidItem = command.Items[0] with { Material = (ElectronicMaterial)0 };
        var invalidCommand = command with { Items = [invalidItem] };

        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(invalidCommand, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    private static RegisterDiscard.Command ValidCommand(Guid? pointId = null) =>
        new()
        {
            QrCode = $"ecocell://pc/{pointId ?? Guid.NewGuid():D}",
            Items =
            [
                new RegisterDiscard.ItemCommand
                {
                    Material = Material,
                    Quantity = 1,
                    ApproximateWeightKg = 0.250m,
                },
            ],
        };
}

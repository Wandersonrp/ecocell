using Ecocell.Shared.Utils;
using Shouldly;

namespace Ecocell.UnitTests.Shared.Utils;

public sealed class CollectorPointQrCodeTests
{
    private const string Id = "018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10";

    [Fact]
    public void TryParse_ShouldReturnId_WhenValueIsCanonical()
    {
        var parsed = CollectorPointQrCode.TryParse($"ecocell://pc/{Id}", out var result);

        parsed.ShouldBeTrue();
        result.ShouldBe(Guid.Parse(Id));
    }

    [Fact]
    public void TryParse_ShouldAllowDifferentSchemeAndHostCasing()
    {
        CollectorPointQrCode.TryParse($"EcOcElL://PC/{Id}", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-qr")]
    [InlineData("ecocell://pc/018F3F2A-7B4C-7C91-8D31-5F7A2B6C9E10")]
    [InlineData("ecocell://pc/x/../018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
    public void TryParse_ShouldReturnFalse_WhenValueIsNotCanonical(string? value)
    {
        CollectorPointQrCode.TryParse(value, out var result).ShouldBeFalse();
        result.ShouldBe(Guid.Empty);
    }
}

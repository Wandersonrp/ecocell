using Ecocell.Api.Features.Map;
using Shouldly;

namespace Ecocell.UnitTests.Features.Map;

public class SearchNearbyPointsTests : TestBase
{
    private readonly SearchNearbyPoints.Validator _validator = new();

    [Fact]
    public void Validator_ShouldPass_WhenProximityModeIsValid()
    {
        var command = new SearchNearbyPoints.Command
        {
            Latitude = -23.5m,
            Longitude = -46.6m,
            RadiusKm = 10
        };

        _validator.Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_ShouldPass_WhenCityModeIsValid()
    {
        var command = new SearchNearbyPoints.Command { City = "São Paulo" };

        _validator.Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_ShouldFail_WhenNoModeProvided()
    {
        var command = new SearchNearbyPoints.Command();

        _validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_ShouldFail_WhenBothModesProvided()
    {
        var command = new SearchNearbyPoints.Command
        {
            Latitude = -23.5m,
            Longitude = -46.6m,
            City = "São Paulo"
        };

        _validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Validator_ShouldFail_WhenLatitudeOutOfRange(decimal latitude)
    {
        var command = new SearchNearbyPoints.Command { Latitude = latitude, Longitude = 0 };

        _validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Validator_ShouldFail_WhenRadiusOutOfRange(double radiusKm)
    {
        var command = new SearchNearbyPoints.Command { Latitude = -23.5m, Longitude = -46.6m, RadiusKm = radiusKm };

        _validator.Validate(command).IsValid.ShouldBeFalse();
    }
}

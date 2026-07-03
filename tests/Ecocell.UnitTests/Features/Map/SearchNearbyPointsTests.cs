using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Map;
using Ecocell.Api.Shared;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Ecocell.UnitTests.Features.Map;

public class SearchNearbyPointsTests : TestBase
{
    private readonly SearchNearbyPoints.Validator _validator = new();

    private SearchNearbyPoints.Handler CreateHandler() =>
        new(DbContext, CreateLoggerMock<SearchNearbyPoints.Handler>().Object, _validator);

    private async Task SeedCollectPointAsync(string city, decimal lat, decimal lng, PersonStatus status = PersonStatus.Active)
    {
        var address = new Address("Rua A", "100", "Centro", city, "SP", "01000000", latitude: lat, longitude: lng);
        var legalPerson = new LegalPerson("Razão LTDA", "EcoPonto " + city, "12345678000199", "pj@teste.com", Journey.CollectPoint, addressId: address.Id);
        if (status == PersonStatus.Active) legalPerson.Approve(Role.Admin);
        DbContext.Addresses.Add(address);
        DbContext.People.Add(legalPerson);
        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ShouldReturnActiveCollectPointsInCity_WhenCityMode()
    {
        // Arrange
        await SeedCollectPointAsync("Campinas", -22.9m, -47.06m);
        var handler = CreateHandler();
        var command = new SearchNearbyPoints.Command { City = "Campinas" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem();
        result.Value[0].City.ShouldBe("Campinas");
        result.Value[0].DistanceKm.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreInactiveOrOtherCity_WhenCityMode()
    {
        // Arrange
        await SeedCollectPointAsync("Campinas", -22.9m, -47.06m, PersonStatus.PendingApproval);
        await SeedCollectPointAsync("Santos", -23.96m, -46.33m);
        var handler = CreateHandler();
        var command = new SearchNearbyPoints.Command { City = "Campinas" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenValidationFails()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SearchNearbyPoints.Command();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

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

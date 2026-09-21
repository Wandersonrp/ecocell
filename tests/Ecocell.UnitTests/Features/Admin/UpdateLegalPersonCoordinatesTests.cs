using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Admin;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Features.Admin;

public class UpdateLegalPersonCoordinatesTests : TestBase
{
    private readonly UpdateLegalPersonCoordinates.Handler _handler;
    private readonly UpdateLegalPersonCoordinates.Validator _validator;
    private readonly UpdateLegalPersonCoordinates.Command _command;
    private readonly LegalPerson _legalPerson;

    public UpdateLegalPersonCoordinatesTests()
    {
        _validator = new UpdateLegalPersonCoordinates.Validator();
        var loggerMock = CreateLoggerMock<UpdateLegalPersonCoordinates.Handler>();
        _handler = new UpdateLegalPersonCoordinates.Handler(DbContext, loggerMock.Object, _validator);

        var faker = new Faker("pt_BR");

        var address = new Address(
            faker.Address.StreetName(),
            faker.Address.BuildingNumber(),
            "Centro",
            faker.Address.City(),
            "SP",
            "01310100");

        _legalPerson = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint,
            address.Id);

        DbContext.Addresses.Add(address);
        DbContext.LegalPeople.Add(_legalPerson);
        DbContext.SaveChanges();

        _command = new UpdateLegalPersonCoordinates.Command
        {
            LegalPersonId = _legalPerson.Id,
            Latitude = -23.5505m,
            Longitude = -46.6333m
        };
    }

    [Fact]
    public async Task Handle_ShouldUpdateCoordinates_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var address = await DbContext.LegalPeople
            .Include(lp => lp.Address)
            .Where(lp => lp.Id == _command.LegalPersonId)
            .Select(lp => lp.Address)
            .FirstOrDefaultAsync();

        address.ShouldNotBeNull();
        address!.Latitude.ShouldBe(_command.Latitude);
        address.Longitude.ShouldBe(_command.Longitude);
        address.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLegalPersonDoesNotExist()
    {
        // Arrange
        _command.LegalPersonId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenAddressIsNull()
    {
        // Arrange
        var lpWithoutAddress = new LegalPerson(
            new Faker().Company.CompanyName(),
            new Faker().Company.CompanyName(),
            new Faker().Company.Cnpj(includeFormatSymbols: false),
            new Faker().Internet.Email(),
            Journey.CollectPoint);

        DbContext.LegalPeople.Add(lpWithoutAddress);
        await DbContext.SaveChangesAsync();
        _command.LegalPersonId = lpWithoutAddress.Id;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public async Task Handle_ShouldReturnValidationError_WhenLatitudeIsOutOfRange(decimal latitude)
    {
        // Arrange
        _command.Latitude = latitude;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public async Task Handle_ShouldReturnValidationError_WhenLongitudeIsOutOfRange(decimal longitude)
    {
        // Arrange
        _command.Longitude = longitude;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }
}
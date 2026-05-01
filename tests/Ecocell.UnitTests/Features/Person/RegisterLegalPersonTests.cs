using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Events;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Services.External;
using Ecocell.Api.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

public class RegisterLegalPersonTests : TestBase
{
    private readonly RegisterLegalPerson.Handler _handler;
    private readonly RegisterLegalPerson.Validator _validator;
    private readonly RegisterLegalPerson.Command _command;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly Mock<IGeocodingService> _geocodingMock;
    private readonly NaturalPerson _responsiblePerson;

    public RegisterLegalPersonTests()
    {
        _validator = new RegisterLegalPerson.Validator();
        var loggerMock = CreateLoggerMock<RegisterLegalPerson.Handler>();
        _publisherMock = new Mock<IPublisher>();
        _geocodingMock = new Mock<IGeocodingService>();

        _geocodingMock
            .Setup(g => g.GeocodeAsync(It.IsAny<GeocodingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultT<GeocodingCoordinates>.Success(new GeocodingCoordinates(-23.5505m, -46.6333m)));

        _handler = new RegisterLegalPerson.Handler(DbContext, loggerMock.Object, _validator, _publisherMock.Object, _geocodingMock.Object);

        _responsiblePerson = new NaturalPerson(
            new Faker().Name.FullName(),
            new Faker().Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Role.User,
            new Faker().Internet.Email(),
            Journey.Depositor);

        DbContext.NaturalPeople.Add(_responsiblePerson);
        DbContext.SaveChanges();

        DbContext.People
            .Where(p => p.Id == _responsiblePerson.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.Active));

        _command = new Faker<RegisterLegalPerson.Command>()
            .RuleFor(x => x.Cnpj, f => f.Company.Cnpj(includeFormatSymbols: false))
            .RuleFor(x => x.LegalName, f => f.Company.CompanyName())
            .RuleFor(x => x.TradeName, f => f.Company.CompanyName())
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.Journey, _ => Journey.CollectPoint)
            .RuleFor(x => x.ResponsiblePersonId, _ => _responsiblePerson.Id)
            .RuleFor(x => x.Address, f => new RegisterLegalPerson.AddressCommand
            {
                Street = f.Address.StreetName(),
                Number = f.Address.BuildingNumber(),
                Neighborhood = "Centro",
                City = f.Address.City(),
                State = "SP",
                ZipCode = "01310100",
                Complement = null
            })
            .Generate();
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValidAsCollectPoint()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople.FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.Journey.ShouldBe(Journey.CollectPoint);
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValidAsCollector()
    {
        // Arrange
        _command.Journey = Journey.Collector;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople.FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.Journey.ShouldBe(Journey.Collector);
    }

    [Fact]
    public async Task Handle_ShouldPersistAddressInDatabase_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople
            .Include(lp => lp.Address)
            .FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);

        lp.ShouldNotBeNull();
        lp.AddressId.ShouldNotBeNull();
        lp.Address.ShouldNotBeNull();
        lp.Address!.City.ShouldBe(_command.Address.City);
        lp.Address.State.ShouldBe(_command.Address.State);
        lp.Address.Latitude.ShouldNotBeNull();
        lp.Address.Longitude.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldSetStatusPendingApproval_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople.FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.PersonStatus.ShouldBe(PersonStatus.PendingApproval);
    }

    [Fact]
    public async Task Handle_ShouldPublishLegalPersonRegistered_WhenSuccess()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _publisherMock.Verify(
            p => p.Publish(
                It.Is<LegalPersonRegistered>(e => e.LegalPersonEmail == _command.Email),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCnpjAlreadyExists()
    {
        // Arrange
        var address = new Address("Rua A", "1", "Centro", "São Paulo", "SP", "01310100");
        var existing = new LegalPerson(
            legalName: "Empresa Existente",
            tradeName: "Empresa Existente",
            cnpj: _command.Cnpj,
            email: "outro@empresa.com",
            journey: Journey.CollectPoint,
            addressId: address.Id,
            responsiblePersonId: _responsiblePerson.Id);

        DbContext.Addresses.Add(address);
        DbContext.LegalPeople.Add(existing);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var faker = new Faker();
        string differentCnpj;
        do { differentCnpj = faker.Company.Cnpj(includeFormatSymbols: false); }
        while (differentCnpj == _command.Cnpj);

        var address = new Address("Rua B", "2", "Centro", "São Paulo", "SP", "01310100");
        var existing = new LegalPerson(
            legalName: "Outra Empresa",
            tradeName: "Outra Empresa",
            cnpj: differentCnpj,
            email: _command.Email,
            journey: Journey.CollectPoint,
            addressId: address.Id,
            responsiblePersonId: _responsiblePerson.Id);

        DbContext.Addresses.Add(address);
        DbContext.LegalPeople.Add(existing);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenJourneyIsDepositor()
    {
        // Arrange — RN003: PJ não pode ter Journey.Depositor
        _command.Journey = Journey.Depositor;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("00000000000000")]
    [InlineData("11111111000191")]
    public async Task Handle_ShouldReturnValidationError_WhenCnpjIsInvalid(string cnpj)
    {
        // Arrange
        _command.Cnpj = cnpj;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("@com")]
    [InlineData("empresa.com")]
    public async Task Handle_ShouldReturnValidationError_WhenEmailIsInvalid(string email)
    {
        // Arrange
        _command.Email = email;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_ShouldReturnValidationError_WhenLegalNameIsEmpty(string legalName)
    {
        // Arrange
        _command.LegalName = legalName;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ABC")]
    public async Task Handle_ShouldReturnValidationError_WhenAddressStateIsInvalid(string state)
    {
        // Arrange
        _command.Address.State = state;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("0131010")]
    [InlineData("013101000")]
    public async Task Handle_ShouldReturnValidationError_WhenAddressZipCodeIsInvalid(string zipCode)
    {
        // Arrange
        _command.Address.ZipCode = zipCode;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenResponsiblePersonDoesNotExist()
    {
        // Arrange
        _command.ResponsiblePersonId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenResponsiblePersonIsNotActive()
    {
        // Arrange — reverter o status do gestor para AwaitingConfirmation
        DbContext.People
            .Where(p => p.Id == _responsiblePerson.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.AwaitingConfirmation));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldPersistWithNullCoordinates_WhenGeocodingNotFound()
    {
        // Arrange
        _geocodingMock
            .Setup(g => g.GeocodeAsync(It.IsAny<GeocodingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultT<GeocodingCoordinates>.Failure(
                Error.GeocodingNotFound("endereço inválido")));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople
            .Include(lp => lp.Address)
            .FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.Address.ShouldNotBeNull();
        lp.Address!.Latitude.ShouldBeNull();
        lp.Address.Longitude.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldPersistWithNullCoordinates_WhenGeocodingUnavailable()
    {
        // Arrange
        _geocodingMock
            .Setup(g => g.GeocodeAsync(It.IsAny<GeocodingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultT<GeocodingCoordinates>.Failure(
                new Error(ErrorCodes.GeocodingUnavailable, "Serviço de geocodificação indisponível")));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople
            .Include(lp => lp.Address)
            .FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.Address.ShouldNotBeNull();
        lp.Address!.Latitude.ShouldBeNull();
        lp.Address.Longitude.ShouldBeNull();
    }
}

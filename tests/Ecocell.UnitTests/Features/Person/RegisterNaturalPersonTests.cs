using Bogus;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Shared;
using Bogus.Extensions.Brazil;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

public class RegisterNaturalPersonTests : TestBase
{
    private readonly RegisterNaturalPerson.Handler _handler;
    private readonly RegisterNaturalPerson.Validator _validator;
    private readonly RegisterNaturalPerson.Command _command;

    public RegisterNaturalPersonTests()
    {
        _validator = new RegisterNaturalPerson.Validator();
        var loggerMock = CreateLoggerMock<RegisterNaturalPerson.Handler>();

        _handler = new RegisterNaturalPerson.Handler(DbContext, loggerMock.Object, _validator);

        _command = new Faker<RegisterNaturalPerson.Command>()
            .RuleFor(x => x.FullName, f => f.Name.FullName())
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.Cpf, f => f.Person.Cpf(includeFormatSymbols: false))
            .RuleFor(x => x.Journey, f => f.PickRandom<Journey>())
            .RuleFor(x => x.BirthDate, f => DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)))
            .Generate();
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var exists = await DbContext.NaturalPeople.AnyAsync(np => np.Cpf == _command.Cpf);
        exists.ShouldBeTrue();
    }

    [Theory]
    [InlineData("@com")]
    [InlineData("teste.com")]
    public async Task Handle_ShouldNotPersistInDatabase_WhenEmailIsInvalid(string email)
    {
        // Arrange
        _command.Email = email;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ShouldNotPersistInDatabase_WhenEmailIsTooLong()
    {
        // Arrange
        _command.Email = $"a@{"b".PadRight(253, 'b')}.com"; // 259 chars, acima do limite de 255

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_ShouldNotPersistInDatabase_WhenFullNameIsEmpty(string fullName)
    {
        // Arrange
        _command.FullName = fullName;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AB")]
    public async Task Handle_ShouldNotPersistInDatabase_WhenFullNameIsTooShort(string fullName)
    {
        // Arrange
        _command.FullName = fullName;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ShouldNotPersistInDatabase_WhenFullNameIsTooLong()
    {
        // Arrange
        _command.FullName = new string('A', 101);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ShouldNotPersistInDatabase_WhenJourneyIsInvalid()
    {
        // Arrange
        _command.Journey = (Journey)999;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("12345678901")]
    public async Task Handle_ShouldNotPersistInDatabase_WhenCpfIsInvalid(string cpf)
    {
        // Arrange
        _command.Cpf = cpf;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ShouldNotPersistInDatabase_WhenBirthDateIsUnderMinimumAge()
    {
        // Arrange
        _command.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16).AddDays(1));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenPersonTurns16Today()
    {
        // Arrange — fronteira exata: completa 16 anos hoje
        _command.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var exists = await DbContext.NaturalPeople.AnyAsync(np => np.Cpf == _command.Cpf);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotPersistInDatabase_WhenPersonTurns16Tomorrow()
    {
        // Arrange — um dia antes da fronteira: ainda não completou 16 anos
        _command.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16).AddDays(1));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenPersonTurned16Yesterday()
    {
        // Arrange — um dia após a fronteira: completou 16 anos ontem
        _command.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16).AddDays(-1));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var exists = await DbContext.NaturalPeople.AnyAsync(np => np.Cpf == _command.Cpf);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCpfAlreadyExists()
    {
        // Arrange
        var existing = new NaturalPerson(
            _command.FullName,
            _command.Cpf,
            _command.BirthDate,
            Role.User,
            "outro@email.com",
            Journey.Depositor);

        DbContext.People.Add(existing);
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
        string differentCpf;
        do { differentCpf = faker.Person.Cpf(includeFormatSymbols: false); }
        while (differentCpf == _command.Cpf);

        var existing = new NaturalPerson(
            "Outro Nome",
            differentCpf,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            _command.Email,
            Journey.Depositor);

        DbContext.People.Add(existing);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }
}

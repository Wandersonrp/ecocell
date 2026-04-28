using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

/// <summary>
/// Testes de unidade para o slice UpdateNaturalPerson.
/// </summary>
public class UpdateNaturalPersonTests : TestBase
{
    private readonly UpdateNaturalPerson.Handler _handler;
    private readonly UpdateNaturalPerson.Validator _validator;
    private readonly UpdateNaturalPerson.Command _command;
    private readonly NaturalPerson _existingPerson;

    public UpdateNaturalPersonTests()
    {
        _validator = new UpdateNaturalPerson.Validator();
        var loggerMock = CreateLoggerMock<UpdateNaturalPerson.Handler>();

        _handler = new UpdateNaturalPerson.Handler(DbContext, loggerMock.Object, _validator);

        var faker = new Faker("pt_BR");

        _existingPerson = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);

        DbContext.People.Add(_existingPerson);
        DbContext.SaveChanges();

        _command = new Faker<UpdateNaturalPerson.Command>()
            .RuleFor(x => x.PersonId, _ => _existingPerson.Id)
            .RuleFor(x => x.FullName, f => f.Name.FullName())
            .Generate();
    }

    [Fact]
    public async Task Handle_ShouldUpdateFullName_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.FullName.ShouldBe(_command.FullName);
        result.Value.Id.ShouldBe(_existingPerson.Id);

        var persisted = await DbContext.NaturalPeople.FirstAsync(np => np.Id == _existingPerson.Id);
        persisted.FullName.ShouldBe(_command.FullName);
        persisted.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenFullNameIsEmpty()
    {
        // Arrange
        _command.FullName = string.Empty;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        result.Error.Messages.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    public async Task Handle_ShouldReturnFailure_WhenFullNameIsTooShort(string fullName)
    {
        // Arrange
        _command.FullName = fullName;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        result.Error.Messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenFullNameIsTooLong()
    {
        // Arrange
        _command.FullName = new string('a', 101);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        result.Error.Messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPersonDoesNotExist()
    {
        // Arrange
        _command.PersonId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
        result.Error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenPersonIdIsEmpty()
    {
        // Arrange
        _command.PersonId = Guid.Empty;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        result.Error.Messages.ShouldHaveSingleItem();
    }
}
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

public class ConfirmAccountTests : TestBase
{
    private const string ValidCode = "123456";

    private readonly ConfirmAccount.Handler _handler;
    private readonly ConfirmAccount.Command _command;
    private readonly InMemoryVerificationCodeStore _store;
    private readonly string _key;

    public ConfirmAccountTests()
    {
        _store = new InMemoryVerificationCodeStore();
        var validator = new ConfirmAccount.Validator();
        var loggerMock = CreateLoggerMock<ConfirmAccount.Handler>();

        _handler = new ConfirmAccount.Handler(DbContext, _store, validator, loggerMock.Object);

        var faker = new Faker();
        var person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);

        DbContext.People.Add(person);
        DbContext.SaveChanges();

        _key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.EmailConfirmation, person.Email);
        _store.Seed(_key, VerificationCodeGenerator.Hash(ValidCode), DateTimeOffset.UtcNow.AddMinutes(10));

        _command = new ConfirmAccount.Command
        {
            Email = person.Email,
            Code = ValidCode
        };
    }

    [Fact]
    public async Task Handle_ShouldConfirmAccount_WhenCodeIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var person = await DbContext.People.FirstOrDefaultAsync(p => p.Email == _command.Email);
        person!.PersonStatus.ShouldBe(PersonStatus.Active);
        _store.HasActiveEntry(_key).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenEmailDoesNotExist()
    {
        // Arrange
        _command.Email = new Faker().Internet.Email();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenAccountAlreadyActive()
    {
        // Arrange — confirma a conta na primeira chamada; semeia novo código para chegar ao branch de status
        await _handler.Handle(_command, CancellationToken.None);
        _store.Seed(_key, VerificationCodeGenerator.Hash(ValidCode), DateTimeOffset.UtcNow.AddMinutes(10));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenCodeIsExpired()
    {
        // Arrange
        _store.Seed(_key, VerificationCodeGenerator.Hash(ValidCode), DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.InvalidCredential);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenCodeIsNotFound()
    {
        // Arrange
        await _store.DeleteAsync(_key);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.InvalidCredential);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenAttemptsExceeded()
    {
        // Arrange — 3 tentativas inválidas já registradas
        _store.Seed(_key, VerificationCodeGenerator.Hash(ValidCode), DateTimeOffset.UtcNow.AddMinutes(10), attempts: 3);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenCodeDoesNotMatch()
    {
        // Arrange
        _command.Code = "000000";

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.InvalidCredential);
    }

    [Fact]
    public async Task Handle_ShouldIncrementAttempts_WhenCodeDoesNotMatch()
    {
        // Arrange
        _command.Code = "000000";

        // Act
        await _handler.Handle(_command, CancellationToken.None);

        // Assert
        _store.GetAttempts(_key).ShouldBe(1);
    }

    [Theory]
    [InlineData("naoemail")]
    [InlineData("@dominio.com")]
    [InlineData("")]
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
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abc123")]
    [InlineData("")]
    public async Task Handle_ShouldReturnValidationError_WhenCodeFormatIsInvalid(string code)
    {
        // Arrange
        _command.Code = code;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }
}

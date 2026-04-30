using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Account;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Account;

public class ResendVerificationCodeTests : TestBase
{
    private readonly ResendVerificationCode.Handler _handler;
    private readonly ResendVerificationCode.Command _command;
    private readonly InMemoryVerificationCodeStore _store;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly string _key;

    public ResendVerificationCodeTests()
    {
        _store = new InMemoryVerificationCodeStore();
        _emailSenderMock = new Mock<IEmailSender>();
        var validator = new ResendVerificationCode.Validator();
        var loggerMock = CreateLoggerMock<ResendVerificationCode.Handler>();

        _handler = new ResendVerificationCode.Handler(
            DbContext, _store, _emailSenderMock.Object, validator, loggerMock.Object);

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

        _command = new ResendVerificationCode.Command { Email = person.Email };
    }

    [Fact]
    public async Task Handle_ShouldSaveNewCodeInStore_WhenAccountIsAwaitingConfirmation()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _store.HasActiveEntry(_key).ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenAccountIsAwaitingConfirmation()
    {
        // Act
        await _handler.Handle(_command, CancellationToken.None);

        // Assert
        _emailSenderMock.Verify(
            s => s.SendAsync(
                _command.Email,
                EmailType.VerificationCode,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldOverwritePreviousCode_WhenCodeAlreadyExists()
    {
        // Arrange — código anterior com 2 tentativas inválidas já registradas
        _store.Seed(_key, VerificationCodeGenerator.Hash("999999"), DateTimeOffset.UtcNow.AddMinutes(5), attempts: 2);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert — novo código ativo com tentativas zeradas
        result.IsSuccess.ShouldBeTrue();
        _store.HasActiveEntry(_key).ShouldBeTrue();
        _store.GetAttempts(_key).ShouldBe(0);
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
        // Arrange — ativar a conta diretamente via método interno
        var person = await DbContext.People.FirstOrDefaultAsync(p => p.Email == _command.Email);
        person!.Confirm();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
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
}
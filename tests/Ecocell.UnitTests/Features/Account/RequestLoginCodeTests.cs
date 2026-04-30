using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Configurations;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Account;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.UnitTests.Helpers;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Account;

public class RequestLoginCodeTests : TestBase
{
    private static readonly IOptions<JwtSettings> TestJwtOptions = Options.Create(new JwtSettings
    {
        SigningKey = "test-hmac-signing-key-ecocell-32chars!!",
        Issuer = "test",
        Audience = "test",
        AccessTokenLifetimeMinutes = 60
    });

    private readonly RequestLoginCode.Handler _handler;
    private readonly RequestLoginCode.Command _command;
    private readonly InMemoryVerificationCodeStore _store;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly string _key;

    public RequestLoginCodeTests()
    {
        _store = new InMemoryVerificationCodeStore();
        _emailSenderMock = new Mock<IEmailSender>();
        var validator = new RequestLoginCode.Validator();
        var loggerMock = CreateLoggerMock<RequestLoginCode.Handler>();

        _handler = new RequestLoginCode.Handler(
            DbContext, _store, _emailSenderMock.Object, validator, loggerMock.Object, TestJwtOptions);

        var faker = new Faker();
        var person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);

        person.Confirm();

        DbContext.People.Add(person);
        DbContext.SaveChanges();

        _key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.Login, person.Email);
        _command = new RequestLoginCode.Command { Email = person.Email };
    }

    [Fact]
    public async Task Handle_ShouldGenerateAndSendCode_WhenPersonIsActive()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _store.HasActiveEntry(_key).ShouldBeTrue();
        _emailSenderMock.Verify(
            s => s.SendAsync(
                _command.Email,
                EmailType.VerificationCode,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSilentlySucceedAndNotSendCode_WhenPersonNotFound()
    {
        // Arrange
        _command.Email = new Faker().Internet.Email();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _emailSenderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<EmailType>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSilentlySucceedAndNotSendCode_WhenPersonIsAwaitingConfirmation()
    {
        // Arrange — insere nova pessoa sem confirmar (AwaitingConfirmation por padrão)
        var faker = new Faker();
        var inactivePerson = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);

        DbContext.People.Add(inactivePerson);
        await DbContext.SaveChangesAsync();

        _command.Email = inactivePerson.Email;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _emailSenderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<EmailType>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldStillReturnSuccess_WhenEmailSenderThrows()
    {
        // Arrange
        _emailSenderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<EmailType>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponível"));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
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
        _emailSenderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<EmailType>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

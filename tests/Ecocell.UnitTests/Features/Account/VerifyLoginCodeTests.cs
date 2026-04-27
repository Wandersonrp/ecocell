using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Configurations;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Account;
using Ecocell.Api.Services.Authentication;
using PersonEntity = Ecocell.Api.Entities.Person;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.UnitTests.Helpers;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Account;

public class VerifyLoginCodeTests : TestBase
{
    private const string ValidCode = "654321";

    private static readonly IOptions<JwtSettings> TestJwtOptions = Options.Create(new JwtSettings
    {
        SigningKey = "test-hmac-signing-key-ecocell-32chars!!",
        Issuer = "test",
        Audience = "test",
        AccessTokenLifetimeMinutes = 60
    });

    private readonly VerifyLoginCode.Handler _handler;
    private readonly VerifyLoginCode.Command _command;
    private readonly InMemoryVerificationCodeStore _store;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly string _key;

    public VerifyLoginCodeTests()
    {
        _store = new InMemoryVerificationCodeStore();

        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _jwtTokenServiceMock
            .Setup(s => s.Generate(It.IsAny<PersonEntity>()))
            .Returns(new JwtToken("fake.jwt", DateTimeOffset.UtcNow.AddHours(1)));

        var validator = new VerifyLoginCode.Validator();
        var loggerMock = CreateLoggerMock<VerifyLoginCode.Handler>();

        _handler = new VerifyLoginCode.Handler(
            DbContext, _store, _jwtTokenServiceMock.Object, validator, loggerMock.Object, TestJwtOptions);

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
        _store.Seed(
            _key,
            VerificationCodeGenerator.HashHmac(ValidCode, TestJwtOptions.Value.SigningKey),
            DateTimeOffset.UtcNow.AddMinutes(10));

        _command = new VerifyLoginCode.Command
        {
            Email = person.Email,
            Code = ValidCode
        };
    }

    [Fact]
    public async Task Handle_ShouldReturnAccessToken_WhenCodeIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("fake.jwt");
        result.Value.TokenType.ShouldBe("Bearer");
        _store.HasActiveEntry(_key).ShouldBeFalse();
        _jwtTokenServiceMock.Verify(s => s.Generate(It.IsAny<PersonEntity>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenPersonNotFound()
    {
        // Arrange
        _command.Email = new Faker().Internet.Email();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.InvalidCredential);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenPersonIsNotActive()
    {
        // Arrange — insere pessoa sem confirmar (AwaitingConfirmation por padrão)
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
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenStoreIsEmpty()
    {
        // Arrange — remove a entrada do store
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
        // Arrange — seed com 3 tentativas inválidas registradas
        _store.Seed(
            _key,
            VerificationCodeGenerator.HashHmac(ValidCode, TestJwtOptions.Value.SigningKey),
            DateTimeOffset.UtcNow.AddMinutes(10),
            attempts: 3);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenCodeIsWrong()
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
    public async Task Handle_ShouldIncrementAttempts_WhenCodeIsWrong()
    {
        // Arrange
        _command.Code = "000000";

        // Act
        await _handler.Handle(_command, CancellationToken.None);

        // Assert
        _store.GetAttempts(_key).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidCredential_WhenCodeIsExpired()
    {
        // Arrange
        _store.Seed(
            _key,
            VerificationCodeGenerator.HashHmac(ValidCode, TestJwtOptions.Value.SigningKey),
            DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.InvalidCredential);
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

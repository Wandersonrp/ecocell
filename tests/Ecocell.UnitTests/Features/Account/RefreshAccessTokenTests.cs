using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Account;
using Ecocell.Api.Services.Authentication;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;
using PersonEntity = Ecocell.Api.Entities.Person;

namespace Ecocell.UnitTests.Features.Account;

public class RefreshAccessTokenTests : TestBase
{
    // Token válido de 64 chars hex (32 bytes) — representativo, não precisa ser real
    private const string ValidRefreshToken = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

    private readonly RefreshAccessToken.Handler _handler;
    private readonly RefreshAccessToken.Command _command;
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Guid _personId;

    public RefreshAccessTokenTests()
    {
        _refreshTokenStoreMock = new Mock<IRefreshTokenStore>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();

        // Pré-popula DbContext com pessoa ativa
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
        _personId = person.Id;

        // Setup padrão — caminho feliz
        _refreshTokenStoreMock
            .Setup(s => s.GetPersonIdAsync(ValidRefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_personId);
        _refreshTokenStoreMock
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokenStoreMock
            .Setup(s => s.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jwtTokenServiceMock
            .Setup(s => s.Generate(It.IsAny<PersonEntity>()))
            .Returns(new JwtToken("new.jwt.token", DateTimeOffset.UtcNow.AddHours(1)));
        _jwtTokenServiceMock
            .Setup(s => s.GenerateRefreshToken())
            .Returns(new RefreshTokenResult(
                "11223344556677889900aabbccddeeff11223344556677889900aabbccddeeff",
                DateTimeOffset.UtcNow.AddDays(7)));

        var validator = new RefreshAccessToken.Validator();
        var loggerMock = CreateLoggerMock<RefreshAccessToken.Handler>();

        _handler = new RefreshAccessToken.Handler(
            DbContext,
            _refreshTokenStoreMock.Object,
            _jwtTokenServiceMock.Object,
            validator,
            loggerMock.Object);

        _command = new RefreshAccessToken.Command
        {
            RefreshToken = ValidRefreshToken
        };
    }

    [Fact]
    public async Task Handle_ShouldReturnNewTokenPair_WhenRefreshTokenIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("new.jwt.token");
        result.Value.RefreshToken.ShouldBe("11223344556677889900aabbccddeeff11223344556677889900aabbccddeeff");
        result.Value.RefreshTokenExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        _jwtTokenServiceMock.Verify(s => s.Generate(It.IsAny<PersonEntity>()), Times.Once);
        _jwtTokenServiceMock.Verify(s => s.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldInvalidateOldToken_WhenRefreshTokenIsUsed()
    {
        // Arrange — rastreia ordem de chamadas (delete deve ocorrer antes do save)
        var callOrder = new List<string>();
        _refreshTokenStoreMock
            .Setup(s => s.DeleteAsync(ValidRefreshToken, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("delete"))
            .Returns(Task.CompletedTask);
        _refreshTokenStoreMock
            .Setup(s => s.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(_command, CancellationToken.None);

        // Assert
        callOrder.ShouldBe(["delete", "save"]);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        _refreshTokenStoreMock
            .Setup(s => s.GetPersonIdAsync(ValidRefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.InvalidCredential);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenPersonIsNotActive()
    {
        // Arrange — insere pessoa não confirmada e aponta o store para ela
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

        _refreshTokenStoreMock
            .Setup(s => s.GetPersonIdAsync(ValidRefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactivePerson.Id);

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenRefreshTokenIsEmpty()
    {
        // Arrange
        _command.RefreshToken = string.Empty;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }
}

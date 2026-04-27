using Bogus;
using Ecocell.Api.Events;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.UnitTests.Helpers;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

public class GenerateVerificationCodeOnPersonRegisteredTests : TestBase
{
    private readonly GenerateVerificationCodeOnPersonRegistered.Handler _handler;
    private readonly InMemoryVerificationCodeStore _store;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly PersonRegistered _notification;

    public GenerateVerificationCodeOnPersonRegisteredTests()
    {
        _store = new InMemoryVerificationCodeStore();
        _emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = CreateLoggerMock<GenerateVerificationCodeOnPersonRegistered.Handler>();

        _handler = new GenerateVerificationCodeOnPersonRegistered.Handler(
            _store, _emailSenderMock.Object, loggerMock.Object);

        _notification = new PersonRegistered(Guid.NewGuid(), new Faker().Internet.Email());
    }

    [Fact]
    public async Task Handle_ShouldSaveCodeInStore_WhenPersonRegistered()
    {
        // Act
        await _handler.Handle(_notification, CancellationToken.None);

        // Assert
        var key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.EmailConfirmation, _notification.Email);
        _store.HasActiveEntry(key).ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenPersonRegistered()
    {
        // Act
        await _handler.Handle(_notification, CancellationToken.None);

        // Assert
        _emailSenderMock.Verify(
            s => s.SendVerificationCodeAsync(
                _notification.Email,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotThrow_WhenEmailSenderFails()
    {
        // Arrange
        _emailSenderMock
            .Setup(s => s.SendVerificationCodeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponível."));

        // Act & Assert
        var act = async () => await _handler.Handle(_notification, CancellationToken.None);
        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldNotThrow_WhenStoreFails()
    {
        // Arrange
        var failingStoreMock = new Mock<IVerificationCodeStore>();
        failingStoreMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis indisponível."));

        var loggerMock = CreateLoggerMock<GenerateVerificationCodeOnPersonRegistered.Handler>();
        var handler = new GenerateVerificationCodeOnPersonRegistered.Handler(
            failingStoreMock.Object, _emailSenderMock.Object, loggerMock.Object);

        // Act & Assert
        var act = async () => await handler.Handle(_notification, CancellationToken.None);
        await act.ShouldNotThrowAsync();
    }
}

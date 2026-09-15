using Ecocell.Api.Features.CollectorPoint;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class GeneratePcQrCodeTests : TestBase
{
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();
    private readonly GeneratePcQrCode.Handler _handler;

    public static TheoryData<Error> AccessErrors => new()
    {
        Error.Forbidden(),
        Error.NotFound("Ponto de coleta não encontrado."),
        Error.Conflict("Ponto de coleta não está ativo."),
    };

    public GeneratePcQrCodeTests()
    {
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _handler = new GeneratePcQrCode.Handler(
            CreateLoggerMock<GeneratePcQrCode.Handler>().Object,
            new GeneratePcQrCode.Validator(),
            _guard.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnQrString_WhenAccessGuardSucceeds()
    {
        var collectorPointId = Guid.NewGuid();

        var result = await _handler.Handle(
            new GeneratePcQrCode.Query(collectorPointId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Qr.ShouldBe($"ecocell://pc/{collectorPointId}");
        _guard.Verify(x => x.EnsureResponsibleActiveAsync(
            collectorPointId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(AccessErrors))]
    public async Task Handle_ShouldPropagateAccessError_WhenGuardFails(Error error)
    {
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(error));

        var result = await _handler.Handle(
            new GeneratePcQrCode.Query(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationErrorWithoutCallingGuard_WhenIdIsEmpty()
    {
        var result = await _handler.Handle(
            new GeneratePcQrCode.Query(Guid.Empty),
            CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        _guard.Verify(x => x.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Discard;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Discard;

public class RejectDiscardTests : TestBase
{
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();

    public RejectDiscardTests()
    {
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    public static TheoryData<Error> AccessErrors => new()
    {
        Error.Forbidden(),
        Error.NotFound("Ponto de coleta não encontrado."),
        Error.Conflict("Ponto de coleta não está ativo."),
    };

    [Fact]
    public async Task Handle_ShouldPersistRejectedWithoutCreditRequest_WhenDiscardIsPending()
    {
        var discard = AddPendingDiscard();

        var result = await CreateHandler().Handle(
            new RejectDiscard.Command(discard.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var persisted = await DbContext.Discards.SingleAsync(value => value.Id == discard.Id);
        persisted.Status.ShouldBe(DiscardStatus.Rejected);
        (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationErrorWithoutCallingGuard_WhenIdIsEmpty()
    {
        var result = await CreateHandler().Handle(
            new RejectDiscard.Command(Guid.Empty),
            CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        _guard.Verify(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFoundWithoutCallingGuard_WhenDiscardDoesNotExist()
    {
        var result = await CreateHandler().Handle(
            new RejectDiscard.Command(Guid.NewGuid()),
            CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
        _guard.Verify(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(AccessErrors))]
    public async Task Handle_ShouldPropagateAccessError_WhenGuardFails(Error error)
    {
        var discard = AddPendingDiscard();
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(
                discard.CollectorPointId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(error));

        var result = await CreateHandler().Handle(
            new RejectDiscard.Command(discard.Id),
            CancellationToken.None);

        result.Error.ShouldBe(error);
        DbContext.ChangeTracker.Clear();
        (await DbContext.Discards.SingleAsync(value => value.Id == discard.Id))
            .Status.ShouldBe(DiscardStatus.Pending);
    }

    [Theory]
    [InlineData(DiscardStatus.Confirmed)]
    [InlineData(DiscardStatus.Rejected)]
    public async Task Handle_ShouldReturnConflict_WhenDiscardIsTerminal(DiscardStatus status)
    {
        var discard = AddPendingDiscard();
        if (status == DiscardStatus.Confirmed)
            discard.Confirm(discard.Items.ToArray());
        else
            discard.Reject();
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new RejectDiscard.Command(discard.Id),
            CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldAuthorizeBeforeRevealingTerminalStatus()
    {
        var discard = AddPendingDiscard();
        discard.Reject();
        await DbContext.SaveChangesAsync();
        var hidden = Error.NotFound("Ponto de coleta não encontrado.");
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(
                discard.CollectorPointId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(hidden));

        var result = await CreateHandler().Handle(
            new RejectDiscard.Command(discard.Id),
            CancellationToken.None);

        result.Error.ShouldBe(hidden);
    }

    private RejectDiscard.Handler CreateHandler() =>
        new(
            DbContext,
            CreateLoggerMock<RejectDiscard.Handler>().Object,
            new RejectDiscard.Validator(),
            _guard.Object);

    private Ecocell.Api.Entities.Discard AddPendingDiscard()
    {
        var faker = new Faker("pt_BR");
        var depositor = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);
        var point = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint,
            responsiblePersonId: depositor.Id);
        point.Approve(Role.Admin);
        var rule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var discard = new Ecocell.Api.Entities.Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.Battery, 1, 0.250m, rule.Id)]);

        DbContext.AddRange(depositor, point, rule, discard);
        DbContext.SaveChanges();
        return discard;
    }
}

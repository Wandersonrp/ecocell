using System.Globalization;
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

public class ConfirmDiscardTests : TestBase
{
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();
    private readonly ConfirmDiscard.Handler _handler;

    public ConfirmDiscardTests()
    {
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _handler = new ConfirmDiscard.Handler(
            DbContext,
            CreateLoggerMock<ConfirmDiscard.Handler>().Object,
            new ConfirmDiscard.Validator(),
            _guard.Object);
    }

    private static ElectronicMaterial Material =>
        Enum.GetValues<ElectronicMaterial>()
            .First(value => Convert.ToInt32(value) > 0);

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand(), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenDiscardIdIsEmpty()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { DiscardId = Guid.Empty }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
    {
        var item = ValidItem() with { Quantity = quantity };
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    [InlineData("0.0001")]
    [InlineData("10000000.000")]
    public async Task Validate_ShouldFail_WhenWeightIsInvalid(string rawWeight)
    {
        var weight = decimal.Parse(rawWeight, CultureInfo.InvariantCulture);
        var item = ValidItem() with { ApproximateWeightKg = weight };
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreNull()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = null! }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreEmpty()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemIsNull()
    {
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [null!] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
    {
        var item = ValidItem() with { Material = (ElectronicMaterial)0 };
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
    {
        var item = ValidItem();
        var result = await new ConfirmDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item, item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPersistFinalItemsAndCreditRequest_WhenCommandIsValid()
    {
        // Arrange
        var fixture = AddPendingDiscard();
        var finalMaterial = SecondMaterial();
        var finalRule = AddRule(
            fixture.CollectorPointId,
            finalMaterial,
            fixture.Discard.CreatedAt.AddMinutes(-1));
        var command = ValidCommand(fixture.Discard.Id) with
        {
            Items =
            [
                new ConfirmDiscard.ItemCommand
                {
                    Material = finalMaterial,
                    Quantity = 3,
                    ApproximateWeightKg = 0.750m,
                },
            ],
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var persisted = await DbContext.Discards
            .Include(value => value.Items)
            .SingleAsync(value => value.Id == fixture.Discard.Id);
        persisted.Status.ShouldBe(DiscardStatus.Confirmed);
        persisted.Items.ShouldHaveSingleItem();
        persisted.Items.Single().Material.ShouldBe(finalMaterial);
        persisted.Items.Single().Quantity.ShouldBe(3);
        persisted.Items.Single().ApproximateWeightKg.ShouldBe(0.750m);
        persisted.Items.Single().MaterialScoreRuleId.ShouldBe(finalRule.Id);
        (await DbContext.CreditScoreRequests.CountAsync(
            value => value.DiscardId == fixture.Discard.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ShouldUseRuleActiveAtOpening_WhenRuleWasVersionedLater()
    {
        // Arrange
        var fixture = AddPendingDiscard();
        var original = AddRule(
            fixture.CollectorPointId,
            SecondMaterial(),
            fixture.Discard.CreatedAt.AddHours(-1),
            fixture.Discard.CreatedAt.AddMinutes(1));
        AddRule(
            fixture.CollectorPointId,
            SecondMaterial(),
            fixture.Discard.CreatedAt.AddMinutes(1));

        // Act
        var result = await _handler.Handle(
            CommandFor(fixture.Discard.Id, SecondMaterial()),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var item = await DbContext.DiscardItems.SingleAsync();
        item.MaterialScoreRuleId.ShouldBe(original.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictWithoutWriting_WhenMaterialHadNoRuleAtOpening()
    {
        // Arrange
        var fixture = AddPendingDiscard();
        var material = SecondMaterial();
        AddRule(
            fixture.CollectorPointId,
            material,
            fixture.Discard.CreatedAt.AddMinutes(1));

        // Act
        var result = await _handler.Handle(
            CommandFor(fixture.Discard.Id, material),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        DbContext.ChangeTracker.Clear();
        var persisted = await DbContext.Discards
            .Include(value => value.Items)
            .SingleAsync(value => value.Id == fixture.Discard.Id);
        persisted.Status.ShouldBe(DiscardStatus.Pending);
        (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationErrorWithoutCallingGuard_WhenCommandIsInvalid()
    {
        // Act
        var result = await _handler.Handle(
            ValidCommand() with { DiscardId = Guid.Empty },
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        _guard.Verify(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenDiscardDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            ValidCommand(),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
        _guard.Verify(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPropagateAccessErrorBeforeCheckingStatus_WhenGuardFails()
    {
        // Arrange
        var fixture = AddPendingDiscard();
        fixture.Discard.Reject();
        await DbContext.SaveChangesAsync();
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(
                fixture.CollectorPointId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("Ponto de coleta não encontrado.")));

        // Act
        var result = await _handler.Handle(
            ValidCommand(fixture.Discard.Id),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictWithoutWriting_WhenDiscardIsTerminal()
    {
        // Arrange
        var fixture = AddPendingDiscard();
        fixture.Discard.Reject();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            ValidCommand(fixture.Discard.Id),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldReplaceMultipleItems_WhenFinalListChangesComposition()
    {
        // Arrange
        var fixture = AddPendingDiscard();
        var second = SecondMaterial();
        var secondRule = AddRule(
            fixture.CollectorPointId,
            second,
            fixture.Discard.CreatedAt.AddMinutes(-1));
        var command = ValidCommand(fixture.Discard.Id) with
        {
            Items =
            [
                new ConfirmDiscard.ItemCommand
                {
                    Material = Material,
                    Quantity = 2,
                    ApproximateWeightKg = 0.500m,
                },
                new ConfirmDiscard.ItemCommand
                {
                    Material = second,
                    Quantity = 1,
                    ApproximateWeightKg = 1.250m,
                },
            ],
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var items = await DbContext.DiscardItems
            .OrderBy(value => value.Material)
            .ToListAsync();
        items.Count.ShouldBe(2);
        items.Single(value => value.Material == second)
            .MaterialScoreRuleId.ShouldBe(secondRule.Id);
    }

    private sealed record DiscardFixture(
        Ecocell.Api.Entities.Discard Discard,
        Guid CollectorPointId,
        Guid DepositorId);

    private DiscardFixture AddPendingDiscard()
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
            Material,
            10m,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var discard = new Ecocell.Api.Entities.Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(Material, 1, 0.250m, rule.Id)]);

        DbContext.NaturalPeople.Add(depositor);
        DbContext.LegalPeople.Add(point);
        DbContext.MaterialScoreRules.Add(rule);
        DbContext.Discards.Add(discard);
        DbContext.SaveChanges();
        return new DiscardFixture(discard, point.Id, depositor.Id);
    }

    private MaterialScoreRule AddRule(
        Guid collectorPointId,
        ElectronicMaterial material,
        DateTime validFrom,
        DateTime? validTo = null)
    {
        var rule = new MaterialScoreRule(
            collectorPointId,
            material,
            10m,
            MaterialScoreUnit.PerUnit,
            validFrom);
        if (validTo is not null)
            rule.Close(validTo.Value);

        DbContext.MaterialScoreRules.Add(rule);
        DbContext.SaveChanges();
        return rule;
    }

    private static ElectronicMaterial SecondMaterial() =>
        Enum.GetValues<ElectronicMaterial>()
            .First(value => value != Material && Convert.ToInt32(value) > 0);

    private static ConfirmDiscard.Command CommandFor(
        Guid discardId,
        ElectronicMaterial material) =>
        new()
        {
            DiscardId = discardId,
            Items =
            [
                new ConfirmDiscard.ItemCommand
                {
                    Material = material,
                    Quantity = 1,
                    ApproximateWeightKg = 0.250m,
                },
            ],
        };

    private static ConfirmDiscard.ItemCommand ValidItem() =>
        new()
        {
            Material = Material,
            Quantity = 1,
            ApproximateWeightKg = 0.250m,
        };

    private static ConfirmDiscard.Command ValidCommand(Guid? discardId = null) =>
        new()
        {
            DiscardId = discardId ?? Guid.NewGuid(),
            Items = [ValidItem()],
        };
}

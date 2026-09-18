using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.CollectorPoint;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class SetMaterialScoreRulesTests : TestBase
{
    private static readonly DateTime UtcNow = new(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc);
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();

    public SetMaterialScoreRulesTests()
    {
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task Handle_ShouldCreateCurrentRules_WhenCollectorPointHasNoRules()
    {
        // Arrange
        var collectorPoint = AddCollectorPoint();
        var handler = CreateHandler(UtcNow);
        var command = Command(
            collectorPoint.Id,
            Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit),
            Rule(ElectronicMaterial.Notebook, 25m, MaterialScoreUnit.PerKilogram));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rules = await DbContext.MaterialScoreRules.OrderBy(x => x.Material).ToListAsync();
        rules.Count.ShouldBe(2);
        rules.ShouldAllBe(x => x.ValidFrom == UtcNow && x.ValidTo == null);
    }

    [Fact]
    public async Task Handle_ShouldApplyMixedDiffAndPreserveHistory_WhenTableChanges()
    {
        // Arrange
        var collectorPoint = AddCollectorPoint();
        var validFrom = UtcNow.AddHours(-1);
        var battery = AddRule(collectorPoint.Id, ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit, validFrom);
        var notebook = AddRule(collectorPoint.Id, ElectronicMaterial.Notebook, 20m, MaterialScoreUnit.PerUnit, validFrom);
        var printer = AddRule(collectorPoint.Id, ElectronicMaterial.Printer, 30m, MaterialScoreUnit.PerUnit, validFrom);
        var handler = CreateHandler(UtcNow);
        var command = Command(
            collectorPoint.Id,
            Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit),
            Rule(ElectronicMaterial.Notebook, 25m, MaterialScoreUnit.PerKilogram),
            Rule(ElectronicMaterial.CellPhone, 15m, MaterialScoreUnit.PerUnit));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var allRules = await DbContext.MaterialScoreRules
            .Where(x => x.LegalPersonId == collectorPoint.Id)
            .ToListAsync();
        allRules.Count.ShouldBe(5);
        allRules.Count(x => x.ValidTo is null).ShouldBe(3);
        allRules.Single(x => x.Id == battery.Id).ValidTo.ShouldBeNull();
        allRules.Single(x => x.Id == notebook.Id).ValidTo.ShouldBe(UtcNow);
        allRules.Single(x => x.Id == printer.Id).ValidTo.ShouldBe(UtcNow);
        allRules.Single(x => x.Material == ElectronicMaterial.Notebook && x.ValidTo is null)
            .ValidFrom.ShouldBe(UtcNow);
        allRules.Single(x => x.Material == ElectronicMaterial.CellPhone && x.ValidTo is null)
            .ValidFrom.ShouldBe(UtcNow);
    }

    [Fact]
    public async Task Handle_ShouldNotCreateHistory_WhenPayloadMatchesCurrentRules()
    {
        // Arrange
        var collectorPoint = AddCollectorPoint();
        var existing = AddRule(
            collectorPoint.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            UtcNow.AddHours(-1));
        var handler = CreateHandler(UtcNow);

        // Act
        var result = await handler.Handle(
            Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit)),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var persisted = await DbContext.MaterialScoreRules.SingleAsync();
        persisted.Id.ShouldBe(existing.Id);
        persisted.ValidTo.ShouldBeNull();
        persisted.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnWithoutWriting_WhenAccessGuardFails()
    {
        // Arrange
        var collectorPoint = AddCollectorPoint();
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                collectorPoint.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("Ponto de coleta não encontrado.")));
        var handler = CreateHandler(UtcNow);

        // Act
        var result = await handler.Handle(
            Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit)),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
        (await DbContext.MaterialScoreRules.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldAdvanceOneMicrosecond_WhenClockEqualsCurrentValidFrom()
    {
        // Arrange
        var collectorPoint = AddCollectorPoint();
        var current = AddRule(
            collectorPoint.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            UtcNow);
        var handler = CreateHandler(UtcNow);

        // Act
        var result = await handler.Handle(
            Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 20m, MaterialScoreUnit.PerUnit)),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var rules = await DbContext.MaterialScoreRules.OrderBy(x => x.ValidFrom).ToListAsync();
        var effectiveAt = UtcNow.AddTicks(10);
        rules.Single(x => x.Id == current.Id).ValidTo.ShouldBe(effectiveAt);
        rules.Single(x => x.ValidTo is null).ValidFrom.ShouldBe(effectiveAt);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictWithoutWriting_WhenClockRegresses()
    {
        // Arrange
        var collectorPoint = AddCollectorPoint();
        var current = AddRule(
            collectorPoint.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            UtcNow.AddMinutes(1));
        var handler = CreateHandler(UtcNow);

        // Act
        var result = await handler.Handle(
            Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 20m, MaterialScoreUnit.PerUnit)),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        DbContext.ChangeTracker.Clear();
        var persisted = await DbContext.MaterialScoreRules.SingleAsync();
        persisted.Id.ShouldBe(current.Id);
        persisted.Points.ShouldBe(10m);
        persisted.ValidTo.ShouldBeNull();
    }

    private SetMaterialScoreRules.Handler CreateHandler(DateTime utcNow) =>
        new(
            DbContext,
            CreateLoggerMock<SetMaterialScoreRules.Handler>().Object,
            new SetMaterialScoreRules.Validator(),
            _guard.Object,
            new FixedTimeProvider(new DateTimeOffset(utcNow)));

    private LegalPerson AddCollectorPoint()
    {
        var faker = new Faker("pt_BR");
        var collectorPoint = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);
        DbContext.LegalPeople.Add(collectorPoint);
        DbContext.SaveChanges();
        return collectorPoint;
    }

    private MaterialScoreRule AddRule(
        Guid collectorPointId,
        ElectronicMaterial material,
        decimal points,
        MaterialScoreUnit unit,
        DateTime validFrom)
    {
        var rule = new MaterialScoreRule(collectorPointId, material, points, unit, validFrom);
        DbContext.MaterialScoreRules.Add(rule);
        DbContext.SaveChanges();
        return rule;
    }

    private static SetMaterialScoreRules.Command Command(
        Guid collectorPointId,
        params SetMaterialScoreRules.RuleInput[] rules) =>
        new(collectorPointId, rules);

    private static SetMaterialScoreRules.RuleInput Rule(
        ElectronicMaterial material,
        decimal points,
        MaterialScoreUnit unit) =>
        new(material, points, unit);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

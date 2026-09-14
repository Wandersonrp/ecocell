using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Database;

public class MaterialScoreRulePersistenceTests : TestBase
{
    private static readonly DateTime ValidFrom =
        new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Save_ShouldRoundTripRuleAndStoreEnumsAsStrings()
    {
        var collectPoint = CreateCollectPoint();
        var rule = CreateRule(collectPoint.Id);
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.Add(rule);

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var persisted = await DbContext.MaterialScoreRules.SingleAsync(x => x.Id == rule.Id);
        persisted.LegalPersonId.ShouldBe(collectPoint.Id);
        persisted.Material.ShouldBe(ElectronicMaterial.Battery);
        persisted.Points.ShouldBe(10.25m);
        persisted.Unit.ShouldBe(MaterialScoreUnit.PerUnit);
        persisted.ValidFrom.ShouldBe(ValidFrom);
        persisted.ValidTo.ShouldBeNull();

        await using var command = DbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Material", "Unit"
            FROM "MaterialScoreRules"
            WHERE "Id" = $id;
            """;
        command.Parameters.Add(new SqliteParameter("$id", rule.Id));

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        reader.GetString(0).ShouldBe("Battery");
        reader.GetString(1).ShouldBe("PerUnit");
    }

    [Fact]
    public void Model_ShouldConfigureLengthsAndPointsPrecision()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(MaterialScoreRule));
        entityType.ShouldNotBeNull();

        var material = entityType.FindProperty(nameof(MaterialScoreRule.Material));
        var unit = entityType.FindProperty(nameof(MaterialScoreRule.Unit));
        var points = entityType.FindProperty(nameof(MaterialScoreRule.Points));

        material.ShouldNotBeNull();
        unit.ShouldNotBeNull();
        points.ShouldNotBeNull();
        material.GetMaxLength().ShouldBe(50);
        unit.GetMaxLength().ShouldBe(20);
        points.GetPrecision().ShouldBe(10);
        points.GetScale().ShouldBe(2);
    }

    [Fact]
    public async Task Save_ShouldRejectTwoOpenRulesForSameCollectPointAndMaterial()
    {
        var collectPoint = CreateCollectPoint();
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.AddRange(
            CreateRule(collectPoint.Id),
            CreateRule(collectPoint.Id, 20m, ValidFrom.AddMinutes(1)));

        await Should.ThrowAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_ShouldAcceptNewVersionAfterPreviousRuleIsClosed()
    {
        var collectPoint = CreateCollectPoint();
        var firstRule = CreateRule(collectPoint.Id);
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.Add(firstRule);
        await DbContext.SaveChangesAsync();

        var changedAt = ValidFrom.AddHours(1);
        firstRule.Close(changedAt);
        DbContext.MaterialScoreRules.Add(CreateRule(collectPoint.Id, 20m, changedAt));
        await DbContext.SaveChangesAsync();

        (await DbContext.MaterialScoreRules.CountAsync()).ShouldBe(2);
        (await DbContext.MaterialScoreRules.CountAsync(x => x.ValidTo == null)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteLegalPerson_ShouldFail_WhenRuleReferencesIt()
    {
        var collectPoint = CreateCollectPoint();
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.Add(CreateRule(collectPoint.Id));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var persistedPoint = await DbContext.LegalPeople.SingleAsync(x => x.Id == collectPoint.Id);
        DbContext.LegalPeople.Remove(persistedPoint);

        await Should.ThrowAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_ShouldRejectNonPositivePoints()
    {
        var collectPoint = await PersistCollectPointAsync();

        await Should.ThrowAsync<SqliteException>(() =>
            InsertRawRuleAsync(collectPoint.Id, 0m, ValidFrom, null));
    }

    [Fact]
    public async Task Database_ShouldRejectValidToNotAfterValidFrom()
    {
        var collectPoint = await PersistCollectPointAsync();

        await Should.ThrowAsync<SqliteException>(() =>
            InsertRawRuleAsync(collectPoint.Id, 10m, ValidFrom, ValidFrom));
    }

    private async Task<LegalPerson> PersistCollectPointAsync()
    {
        var collectPoint = CreateCollectPoint();
        DbContext.LegalPeople.Add(collectPoint);
        await DbContext.SaveChangesAsync();
        return collectPoint;
    }

    private Task<int> InsertRawRuleAsync(
        Guid legalPersonId,
        decimal points,
        DateTime validFrom,
        DateTime? validTo)
    {
        DateTime? updatedAt = null;

        return DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MaterialScoreRules"
                ("Id", "LegalPersonId", "Material", "Points", "Unit",
                 "ValidFrom", "ValidTo", "CreatedAt", "UpdatedAt")
            VALUES
                ({Guid.NewGuid()}, {legalPersonId}, {"Battery"}, {points}, {"PerUnit"},
                 {validFrom}, {validTo}, {DateTime.UtcNow}, {updatedAt});
            """);
    }

    private static MaterialScoreRule CreateRule(
        Guid legalPersonId,
        decimal points = 10.25m,
        DateTime? validFrom = null) =>
        new(
            legalPersonId,
            ElectronicMaterial.Battery,
            points,
            MaterialScoreUnit.PerUnit,
            validFrom ?? ValidFrom);

    private static LegalPerson CreateCollectPoint()
    {
        var faker = new Faker("pt_BR");

        return new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);
    }
}

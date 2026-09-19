using System.Data;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;

namespace Ecocell.IntegrationTests.Features.Ranking;

public sealed class DepositorRankingViewTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Migration_ShouldCreateViewAndFunctionalIndex()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var viewExists = await ScalarAsync<bool>(db, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_views
                WHERE schemaname = 'public'
                  AND viewname = 'v_ranking_depositor');
            """);
        var indexExists = await ScalarAsync<bool>(db, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname = 'IX_Addresses_State_City_Normalized');
            """);
        var indexDefinition = await ScalarAsync<string>(db, """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = 'IX_Addresses_State_City_Normalized';
            """);

        viewExists.ShouldBeTrue();
        indexExists.ShouldBeTrue();
        indexDefinition.ShouldContain("upper(btrim((\"State\")::text))");
        indexDefinition.ShouldContain("lower(btrim((\"City\")::text))");
    }

    [Fact]
    public async Task View_ShouldRankNationalScoresAndExcludeIneligibleDepositors()
    {
        await CreateDepositorAsync("Ana Ativa", 30m, ApiEnums.PersonStatus.Active);
        await CreateDepositorAsync("Bia Ativa", 30m, ApiEnums.PersonStatus.Active);
        await CreateDepositorAsync("Caio Ativo", 10m, ApiEnums.PersonStatus.Active);
        await CreateDepositorAsync("Davi Suspenso", 100m, ApiEnums.PersonStatus.Suspended);
        await CreateDepositorAsync("Eva Pendente", 100m, ApiEnums.PersonStatus.AwaitingConfirmation);
        await CreateDepositorAsync("Fábio Zerado", 0m, ApiEnums.PersonStatus.Active);

        var rows = await QueryRowsAsync("National");

        rows.Select(row => row.FullName)
            .ShouldBe(["Ana Ativa", "Bia Ativa", "Caio Ativo"]);
        rows.Select(row => row.Position).ShouldBe([1L, 1L, 2L]);
        rows.ShouldAllBe(row => row.State == null && row.City == null);
    }

    [Fact]
    public async Task View_ShouldAggregateMunicipalScoresByNormalizedCityAndState()
    {
        var ana = await CreateDepositorAsync(
            "Ana Municipal",
            35m,
            ApiEnums.PersonStatus.Active);
        var bia = await CreateDepositorAsync(
            "Bia Municipal",
            15m,
            ApiEnums.PersonStatus.Active);

        await CreateCreditAsync(ana.Id, 10m, " São Paulo ", "sp");
        await CreateCreditAsync(ana.Id, 5m, "SÃO PAULO", "SP");
        await CreateCreditAsync(ana.Id, 20m, "São Paulo", "RJ");
        await CreateCreditAsync(bia.Id, 15m, "são paulo", "sp");

        var municipal = await QueryRowsAsync("Municipal");
        var national = await QueryRowsAsync("National");
        var anaSp = municipal.Single(row =>
            row.DepositorId == ana.Id && row.State == "SP" && row.City == "são paulo");
        var biaSp = municipal.Single(row =>
            row.DepositorId == bia.Id && row.State == "SP" && row.City == "são paulo");
        var anaRj = municipal.Single(row =>
            row.DepositorId == ana.Id && row.State == "RJ" && row.City == "são paulo");

        anaSp.TotalPoints.ShouldBe(15m);
        biaSp.TotalPoints.ShouldBe(15m);
        anaSp.Position.ShouldBe(1);
        biaSp.Position.ShouldBe(1);
        anaRj.TotalPoints.ShouldBe(20m);
        anaRj.Position.ShouldBe(1);
        national.Single(row => row.DepositorId == ana.Id).TotalPoints.ShouldBe(35m);
        municipal.Where(row => row.DepositorId == ana.Id)
            .Sum(row => row.TotalPoints)
            .ShouldBe(35m);
    }

    [Fact]
    public async Task View_ShouldKeepNationalScoreAndExcludeMunicipal_WhenLocationIsInvalid()
    {
        var depositor = await CreateDepositorAsync(
            "Ana Sem Município",
            10m,
            ApiEnums.PersonStatus.Active);

        await CreateCreditAsync(
            depositor.Id,
            4m,
            city: null,
            state: null,
            includeAddress: false);
        await CreateCreditAsync(depositor.Id, 6m, " ", " ");

        var municipal = await QueryRowsAsync("Municipal");
        var national = await QueryRowsAsync("National");

        municipal.ShouldNotContain(row => row.DepositorId == depositor.Id);
        national.Single(row => row.DepositorId == depositor.Id)
            .TotalPoints.ShouldBe(10m);
    }

    [Fact]
    public async Task View_ShouldUseCurrentDepositorStatusAndPreserveInactivePointCredits()
    {
        var depositor = await CreateDepositorAsync(
            "Ana Status",
            12m,
            ApiEnums.PersonStatus.Active);
        await CreateCreditAsync(
            depositor.Id,
            12m,
            "Betim",
            "MG",
            suspendCollectorPoint: true);

        var beforeSuspension = await QueryRowsAsync("Municipal");
        beforeSuspension.ShouldContain(row =>
            row.DepositorId == depositor.Id && row.TotalPoints == 12m);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.NaturalPeople.SingleAsync(row => row.Id == depositor.Id);
            stored.Block(ApiEnums.Role.Admin);
            await db.SaveChangesAsync();
        }

        (await QueryRowsAsync("Municipal"))
            .ShouldNotContain(row => row.DepositorId == depositor.Id);
        (await QueryRowsAsync("National"))
            .ShouldNotContain(row => row.DepositorId == depositor.Id);
    }

    private async Task<NaturalPerson> CreateDepositorAsync(
        string fullName,
        decimal totalPoints,
        ApiEnums.PersonStatus status)
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(
            fullName,
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            ApiEnums.Role.User,
            faker.Internet.Email(),
            ApiEnums.Journey.Depositor);

        switch (status)
        {
            case ApiEnums.PersonStatus.Active:
                person.Confirm();
                break;
            case ApiEnums.PersonStatus.Suspended:
                person.Confirm();
                person.Block(ApiEnums.Role.Admin);
                break;
            case ApiEnums.PersonStatus.AwaitingConfirmation:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.People.Add(person);
        if (totalPoints > 0)
            db.DepositorTotalScores.Add(new DepositorTotalScore(person.Id, totalPoints));
        await db.SaveChangesAsync();

        if (totalPoints == 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "DepositorTotalScores"
                    ("Id", "DepositorId", "TotalPoints", "CreatedAt", "UpdatedAt")
                VALUES
                    ({Guid.CreateVersion7()}, {person.Id}, {0m}, {DateTime.UtcNow}, NULL);
                """);
        }

        return person;
    }

    private async Task CreateCreditAsync(
        Guid depositorId,
        decimal points,
        string? city,
        string? state,
        bool includeAddress = true,
        bool suspendCollectorPoint = false)
    {
        var faker = new Faker("pt_BR");
        Address? address = null;
        if (includeAddress)
        {
            address = new Address(
                "Rua Teste",
                "1",
                "Centro",
                city ?? string.Empty,
                state ?? string.Empty,
                "01001000");
        }

        var point = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            ApiEnums.Journey.CollectPoint,
            addressId: address?.Id);
        point.Approve(ApiEnums.Role.Admin);
        if (suspendCollectorPoint)
            point.Block(ApiEnums.Role.Admin);

        var rule = new MaterialScoreRule(
            point.Id,
            ApiEnums.ElectronicMaterial.Battery,
            points,
            ApiEnums.MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var item = new DiscardItem(
            ApiEnums.ElectronicMaterial.Battery,
            1,
            1m,
            rule.Id);
        var discard = new Ecocell.Api.Entities.Discard(depositorId, point.Id, [item]);
        discard.Confirm([item]);
        var transaction = new DepositorScoreTransaction(
            discard.Id,
            depositorId,
            points);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (address is not null)
            db.Addresses.Add(address);
        db.People.Add(point);
        db.MaterialScoreRules.Add(rule);
        db.Discards.Add(discard);
        db.DepositorScoreTransactions.Add(transaction);
        await db.SaveChangesAsync();
    }

    private async Task<List<RankingRow>> QueryRowsAsync(string scopeValue)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Scope", "State", "City", "DepositorId", "FullName",
                   "TotalPoints", "Position"
            FROM v_ranking_depositor
            WHERE "Scope" = @scope
            ORDER BY "Position", "FullName", "DepositorId";
            """;
        var scopeParameter = command.CreateParameter();
        scopeParameter.ParameterName = "scope";
        scopeParameter.DbType = DbType.String;
        scopeParameter.Value = scopeValue;
        command.Parameters.Add(scopeParameter);

        var rows = new List<RankingRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new RankingRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetInt64(6)));
        }

        return rows;
    }

    private static async Task<T> ScalarAsync<T>(AppDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)value!;
    }

    private sealed record RankingRow(
        string Scope,
        string? State,
        string? City,
        Guid DepositorId,
        string FullName,
        decimal TotalPoints,
        long Position);
}

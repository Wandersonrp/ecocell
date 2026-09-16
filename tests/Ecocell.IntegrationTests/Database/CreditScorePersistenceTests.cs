using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Database;

[Collection(nameof(IntegrationTestCollection))]
public class CreditScorePersistenceTests : IntegrationTestBase
{
    public CreditScorePersistenceTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateCreditRequest_ForSameDiscard()
    {
        var seeded = await SeedGraphAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CreditScoreRequests.AddRange(
            new CreditScoreRequest(seeded.DiscardId),
            new CreditScoreRequest(seeded.DiscardId));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateScoreTransaction_ForSameDiscard()
    {
        var seeded = await SeedGraphAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DepositorScoreTransactions.AddRange(
            new DepositorScoreTransaction(seeded.DiscardId, seeded.DepositorId, 10m),
            new DepositorScoreTransaction(seeded.DiscardId, seeded.DepositorId, 10m));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateTotalScore_ForSameDepositor()
    {
        var seeded = await SeedGraphAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DepositorTotalScores.AddRange(
            new DepositorTotalScore(seeded.DepositorId, 10m),
            new DepositorTotalScore(seeded.DepositorId, 20m));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await db.SaveChangesAsync());
    }

    private async Task<PersistenceSeed> SeedGraphAsync()
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
            Journey.CollectPoint);
        var rule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var discard = new Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.Battery, 1, 0.100m, rule.Id)]);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(depositor, point, rule, discard);
        await db.SaveChangesAsync();

        return new PersistenceSeed(depositor.Id, discard.Id);
    }

    private sealed record PersistenceSeed(Guid DepositorId, Guid DiscardId);
}

using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Jobs;

[Collection(nameof(IntegrationTestCollection))]
public class CreditScoreDispatcherTests : IntegrationTestBase
{
    public CreditScoreDispatcherTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task DispatchPending_ShouldContinue_WhenOneRequestFails()
    {
        Guid invalidRequestId;
        Guid validRequestId;
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            invalidRequestId = (await AddRequestAsync(db, confirmed: false)).Id;
            validRequestId = (await AddRequestAsync(db, confirmed: true)).Id;
        }

        var dispatcher = Fixture.Factory.Services.GetRequiredService<CreditScoreDispatcher>();
        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        await using var assertScope = Fixture.Factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await assertDb.CreditScoreRequests.FindAsync(invalidRequestId))!
            .DispatchedAt.ShouldBeNull();
        (await assertDb.CreditScoreRequests.FindAsync(validRequestId))!
            .DispatchedAt.ShouldNotBeNull();
        (await assertDb.DepositorScoreTransactions.CountAsync()).ShouldBe(1);
    }

    private static async Task<CreditScoreRequest> AddRequestAsync(
        AppDbContext db,
        bool confirmed)
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
            [new DiscardItem(ElectronicMaterial.Battery, 2, 0.250m, rule.Id)]);
        var request = new CreditScoreRequest(discard.Id);

        db.NaturalPeople.Add(depositor);
        db.LegalPeople.Add(point);
        db.MaterialScoreRules.Add(rule);
        db.Discards.Add(discard);
        db.CreditScoreRequests.Add(request);
        if (confirmed)
        {
            db.Entry(discard).Property(value => value.Status).CurrentValue =
                DiscardStatus.Confirmed;
        }

        await db.SaveChangesAsync();
        return request;
    }
}

using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Jobs;
using Ecocell.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Jobs;

[Collection(nameof(IntegrationTestCollection))]
public class CreditScoreConcurrencyTests : IntegrationTestBase
{
    public CreditScoreConcurrencyTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Execute_ShouldCreditOnlyOnce_WhenSameRequestRunsConcurrently()
    {
        var requestId = await SeedConfirmedRequestAsync(points: 10m, quantity: 2);
        var interceptor = new CreditScoreWriteBarrierInterceptor(
            "INSERT INTO \"DepositorScoreTransactions\"");
        using var factory = Fixture.CreateDbInterceptedFactory(interceptor);

        var outcomes = await Task.WhenAll(
            ExecuteCapturingAsync(factory.Services, requestId),
            ExecuteCapturingAsync(factory.Services, requestId));

        outcomes.Count(exception => exception is null).ShouldBe(1);
        outcomes.Count(exception => exception is DbUpdateException).ShouldBe(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DepositorScoreTransactions.CountAsync()).ShouldBe(1);
        (await db.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(20m);
        (await db.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Execute_ShouldPreserveSum_WhenDifferentRequestsUpdateSameBalance()
    {
        var seeded = await SeedTwoConfirmedRequestsWithExistingBalanceAsync(
            initialBalance: 5m,
            firstPoints: 10m,
            secondPoints: 20m);
        var interceptor = new CreditScoreWriteBarrierInterceptor(
            "UPDATE \"DepositorTotalScores\"");
        using var factory = Fixture.CreateDbInterceptedFactory(interceptor);

        var outcomes = await Task.WhenAll(
            ExecuteCapturingAsync(factory.Services, seeded.FirstRequestId),
            ExecuteCapturingAsync(factory.Services, seeded.SecondRequestId));

        outcomes.Count(exception => exception is null).ShouldBe(1);
        outcomes.Count(exception => exception is DbUpdateConcurrencyException).ShouldBe(1);

        await using (var retryScope = factory.Services.CreateAsyncScope())
        {
            var retryDb = retryScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendingId = await retryDb.CreditScoreRequests
                .Where(value => value.DispatchedAt == null)
                .Select(value => value.Id)
                .SingleAsync();
            var job = retryScope.ServiceProvider.GetRequiredService<CreditScoreJob>();
            await job.ExecuteAsync(pendingId, CancellationToken.None);
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await assertDb.DepositorScoreTransactions.CountAsync()).ShouldBe(2);
        (await assertDb.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(35m);
        (await assertDb.CreditScoreRequests.CountAsync(
            value => value.DispatchedAt != null)).ShouldBe(2);
    }

    private static async Task<Exception?> ExecuteCapturingAsync(
        IServiceProvider services,
        Guid requestId)
    {
        await using var scope = services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<CreditScoreJob>();
        try
        {
            await job.ExecuteAsync(requestId, CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task<Guid> SeedConfirmedRequestAsync(decimal points, int quantity)
    {
        var faker = new Faker("pt_BR");
        var depositor = CreateDepositor(faker);
        var point = CreateCollectPoint(faker);
        var rule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.Battery,
            points,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var discard = new Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.Battery, quantity, 0.100m, rule.Id)]);
        var request = new CreditScoreRequest(discard.Id);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(depositor, point, rule, discard, request);
        db.Entry(discard).Property(value => value.Status).CurrentValue =
            DiscardStatus.Confirmed;
        await db.SaveChangesAsync();

        return request.Id;
    }

    private async Task<ConcurrentRequestSeed> SeedTwoConfirmedRequestsWithExistingBalanceAsync(
        decimal initialBalance,
        decimal firstPoints,
        decimal secondPoints)
    {
        var faker = new Faker("pt_BR");
        var depositor = CreateDepositor(faker);
        var point = CreateCollectPoint(faker);
        var firstRule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.Battery,
            firstPoints,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var secondRule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.CellPhone,
            secondPoints,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var firstDiscard = new Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.Battery, 1, 0.100m, firstRule.Id)]);
        var secondDiscard = new Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.CellPhone, 1, 0.200m, secondRule.Id)]);
        var firstRequest = new CreditScoreRequest(firstDiscard.Id);
        var secondRequest = new CreditScoreRequest(secondDiscard.Id);
        var balance = new DepositorTotalScore(depositor.Id, initialBalance);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(
            depositor,
            point,
            firstRule,
            secondRule,
            firstDiscard,
            secondDiscard,
            firstRequest,
            secondRequest,
            balance);
        db.Entry(firstDiscard).Property(value => value.Status).CurrentValue =
            DiscardStatus.Confirmed;
        db.Entry(secondDiscard).Property(value => value.Status).CurrentValue =
            DiscardStatus.Confirmed;
        await db.SaveChangesAsync();

        return new ConcurrentRequestSeed(firstRequest.Id, secondRequest.Id);
    }

    private static NaturalPerson CreateDepositor(Faker faker) =>
        new(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);

    private static LegalPerson CreateCollectPoint(Faker faker) =>
        new(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);

    private sealed record ConcurrentRequestSeed(
        Guid FirstRequestId,
        Guid SecondRequestId);
}

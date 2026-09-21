using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Jobs;

public class CreditScoreJobTests : TestBase
{
    private sealed record ScoreItemSeed(
        ElectronicMaterial Material,
        decimal Points,
        MaterialScoreUnit Unit,
        int Quantity,
        decimal WeightKg);

    [Fact]
    public async Task Execute_ShouldCreditPoints_WhenDiscardIsConfirmed()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        var transaction = await DbContext.DepositorScoreTransactions.SingleAsync();
        transaction.DiscardId.ShouldBe(request.DiscardId);
        transaction.Points.ShouldBe(20m);
        (await DbContext.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(20m);
        (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Execute_ShouldUseWeight_WhenRuleIsPerKilogram()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                2.75m,
                MaterialScoreUnit.PerKilogram,
                1,
                0.333m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.SingleAsync())
            .Points.ShouldBe(0.91575m);
        (await DbContext.DepositorTotalScores.SingleAsync())
            .TotalPoints.ShouldBe(0.91575m);
    }

    [Fact]
    public async Task Execute_ShouldSumPoints_WhenDiscardHasMultipleItems()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m),
            new ScoreItemSeed(
                ElectronicMaterial.Notebook,
                3m,
                MaterialScoreUnit.PerKilogram,
                1,
                0.500m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.SingleAsync())
            .Points.ShouldBe(21.50000m);
        (await DbContext.DepositorTotalScores.SingleAsync())
            .TotalPoints.ShouldBe(21.50000m);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Support)]
    public async Task Execute_ShouldNotCreditPoints_WhenPersonIsAdminOrSupport(Role role)
    {
        var request = await AddScoreRequestAsync(
            role,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
        (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
        (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Execute_ShouldReturnWithoutChanges_WhenRequestDoesNotExist()
    {
        await CreateJob().ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
        (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Execute_ShouldReturnWithoutChanges_WhenRequestWasDispatched()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                1,
                0.250m));
        request.MarkAsDispatched(DateTime.UtcNow);
        await DbContext.SaveChangesAsync();

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
        (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Execute_ShouldMarkRequestDispatchedWithoutChangingBalance_WhenTransactionExists()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m));
        var discard = await DbContext.Discards.SingleAsync();
        DbContext.DepositorScoreTransactions.Add(
            new DepositorScoreTransaction(discard.Id, discard.DepositorId, 20m));
        DbContext.DepositorTotalScores.Add(
            new DepositorTotalScore(discard.DepositorId, 20m));
        await DbContext.SaveChangesAsync();

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(1);
        (await DbContext.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(20m);
        (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Execute_ShouldRollbackAndKeepRequestPending_WhenDiscardIsNotConfirmed()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: false,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateJob().ExecuteAsync(request.Id, CancellationToken.None));

        (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
        (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
        (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldBeNull();
    }

    private CreditScoreJob CreateJob() =>
        new(
            DbContext,
            CreateLoggerMock<CreditScoreJob>().Object,
            TimeProvider.System);

    private async Task<CreditScoreRequest> AddScoreRequestAsync(
        Role role,
        bool confirmed,
        params ScoreItemSeed[] seeds)
    {
        var faker = new Faker("pt_BR");
        var depositor = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            role,
            faker.Internet.Email(),
            Journey.Depositor);
        var collectorPoint = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);
        var rules = seeds.Select(seed => new MaterialScoreRule(
            collectorPoint.Id,
            seed.Material,
            seed.Points,
            seed.Unit,
            DateTime.UtcNow.AddDays(-1))).ToArray();
        var items = seeds.Zip(rules, (seed, rule) => new DiscardItem(
            seed.Material,
            seed.Quantity,
            seed.WeightKg,
            rule.Id)).ToArray();
        var discard = new Discard(depositor.Id, collectorPoint.Id, items);
        var request = new CreditScoreRequest(discard.Id);

        DbContext.NaturalPeople.Add(depositor);
        DbContext.LegalPeople.Add(collectorPoint);
        DbContext.MaterialScoreRules.AddRange(rules);
        DbContext.Discards.Add(discard);
        DbContext.CreditScoreRequests.Add(request);
        if (confirmed)
        {
            DbContext.Entry(discard)
                .Property(value => value.Status)
                .CurrentValue = DiscardStatus.Confirmed;
        }

        await DbContext.SaveChangesAsync();
        return request;
    }
}

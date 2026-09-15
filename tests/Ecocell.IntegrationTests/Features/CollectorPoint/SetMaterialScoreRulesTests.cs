using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Shared.Requests.CollectorPoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features.CollectorPoint;

[Collection(nameof(IntegrationTestCollection))]
public class SetMaterialScoreRulesTests : IntegrationTestBase
{
    public SetMaterialScoreRulesTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Put_ShouldReturn401_WhenNoToken()
    {
        var response = await Client.PutAsJsonAsync(
            $"api/collector-points/{Guid.NewGuid()}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ShouldReturn204AndRemainIdempotent_WhenRequestIsValid()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);
        var request = Request(
            Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Notebook, 25m, SharedEnums.MaterialScoreUnit.PerKilogram));

        var first = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            request);
        var second = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            request);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rules = await db.MaterialScoreRules
            .Where(x => x.LegalPersonId == collectorPoint.Id)
            .ToListAsync();
        rules.Count.ShouldBe(2);
        rules.ShouldAllBe(x => x.ValidTo == null);
    }

    [Fact]
    public async Task Put_ShouldReturn400_WhenRulesIsEmpty()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{Guid.NewGuid()}/score-rules",
            new RequestSetMaterialScoreRulesJson { Rules = [] });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ShouldReturn400_WhenEnumValueIsUndefined()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);
        var request = Request(Rule(
            (SharedEnums.ElectronicMaterial)999,
            10m,
            SharedEnums.MaterialScoreUnit.PerUnit));

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{Guid.NewGuid()}/score-rules",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ShouldReturn403_WhenCallerHasPrivilegedRole()
    {
        var (email, jwt) = await CreatePrivilegedUserAndLoginAsync(ApiEnums.Role.Admin);
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_ShouldReturn404_WhenAnotherPersonOwnsCollectorPoint()
    {
        var (ownerEmail, _) = await CreateAndLoginNaturalPersonAsync();
        var ownerId = await GetPersonIdByEmailAsync(ownerEmail);
        var collectorPoint = await CreateActiveCollectorPointAsync(ownerId);
        var (_, callerJwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(callerJwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_ShouldReturn409_WhenOwnedCollectorPointIsPendingApproval()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreatePendingLegalPersonAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_ShouldApplyFullTableDiffAndPreserveHistory_WhenTableChanges()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);
        var firstRequest = Request(
            Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Notebook, 20m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Printer, 30m, SharedEnums.MaterialScoreUnit.PerUnit));
        var secondRequest = Request(
            Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Notebook, 25m, SharedEnums.MaterialScoreUnit.PerKilogram),
            Rule(SharedEnums.ElectronicMaterial.CellPhone, 15m, SharedEnums.MaterialScoreUnit.PerUnit));

        var first = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            firstRequest);
        var second = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            secondRequest);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rules = await db.MaterialScoreRules
            .AsNoTracking()
            .Where(x => x.LegalPersonId == collectorPoint.Id)
            .ToListAsync();
        rules.Count.ShouldBe(5);
        rules.Count(x => x.ValidTo is null).ShouldBe(3);
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.Battery).ValidTo.ShouldBeNull();
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.Printer).ValidTo.ShouldNotBeNull();
        rules.Count(x => x.Material == ApiEnums.ElectronicMaterial.Notebook).ShouldBe(2);
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.Notebook && x.ValidTo is null)
            .Points.ShouldBe(25m);
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.CellPhone && x.ValidTo is null)
            .Points.ShouldBe(15m);
    }

    private static RequestSetMaterialScoreRulesJson Request(
        params RequestMaterialScoreRuleJson[] rules) =>
        new() { Rules = [.. rules] };

    private static RequestMaterialScoreRuleJson Rule(
        SharedEnums.ElectronicMaterial material,
        decimal points,
        SharedEnums.MaterialScoreUnit unit) =>
        new()
        {
            Material = material,
            Points = points,
            Unit = unit,
        };
}

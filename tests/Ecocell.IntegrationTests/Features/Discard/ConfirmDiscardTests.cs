using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Requests.Discards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features.Discard;

public sealed class ConfirmDiscardTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Post_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await Client.PostAsJsonAsync(
            $"api/v1/discards/{Guid.NewGuid()}/confirm",
            ValidRequest(SharedEnums.ElectronicMaterial.Battery));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn204AndPersistFinalItemsAndCreditRequest_WhenRequestIsValid()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(responsibleId);
        var discard = await CreatePendingDiscardAsync(
            responsibleId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsJsonAsync(
            $"api/v1/discards/{discard.Id}/confirm",
            ValidRequest(SharedEnums.ElectronicMaterial.Battery));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Discards
            .Include(value => value.Items)
            .SingleAsync(value => value.Id == discard.Id);
        persisted.Status.ShouldBe(ApiEnums.DiscardStatus.Confirmed);
        persisted.Items.ShouldHaveSingleItem();
        persisted.Items.Single().Quantity.ShouldBe(3);
        persisted.Items.Single().ApproximateWeightKg.ShouldBe(0.750m);
        (await db.CreditScoreRequests.CountAsync(
            value => value.DiscardId == discard.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Post_ShouldReturn404_WhenDiscardBelongsToAnotherResponsible()
    {
        var (_, callerJwt) = await CreateAndLoginNaturalPersonAsync();
        var (ownerEmail, _) = await CreateAndLoginNaturalPersonAsync();
        var ownerId = await GetPersonIdByEmailAsync(ownerEmail);
        var point = await CreateActiveCollectorPointAsync(ownerId);
        var discard = await CreatePendingDiscardAsync(
            ownerId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery);
        using var authClient = CreateAuthenticatedClient(callerJwt);

        var response = await authClient.PostAsJsonAsync(
            $"api/v1/discards/{discard.Id}/confirm",
            ValidRequest(SharedEnums.ElectronicMaterial.Battery));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenDiscardIsTerminal()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(responsibleId);
        var discard = await CreatePendingDiscardAsync(
            responsibleId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await db.Discards.SingleAsync(value => value.Id == discard.Id);
            tracked.Reject();
            await db.SaveChangesAsync();
        }
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsJsonAsync(
            $"api/v1/discards/{discard.Id}/confirm",
            ValidRequest(SharedEnums.ElectronicMaterial.Battery));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenFinalMaterialHadNoRuleAtOpening()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(responsibleId);
        var discard = await CreatePendingDiscardAsync(
            responsibleId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MaterialScoreRules.Add(new MaterialScoreRule(
                point.Id,
                ApiEnums.ElectronicMaterial.Notebook,
                20m,
                ApiEnums.MaterialScoreUnit.PerUnit,
                discard.CreatedAt.AddMinutes(1)));
            await db.SaveChangesAsync();
        }
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsJsonAsync(
            $"api/v1/discards/{discard.Id}/confirm",
            ValidRequest(SharedEnums.ElectronicMaterial.Notebook));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await using var assertScope = Fixture.Factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await assertDb.Discards.SingleAsync(value => value.Id == discard.Id))
            .Status.ShouldBe(ApiEnums.DiscardStatus.Pending);
        (await assertDb.CreditScoreRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Post_ShouldReplaceItemsWithoutUniqueIndexConflict_WhenMaterialsAlreadyExist()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(responsibleId);
        var discard = await CreatePendingDiscardAsync(
            responsibleId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery,
            ApiEnums.ElectronicMaterial.Notebook);
        using var authClient = CreateAuthenticatedClient(jwt);
        var request = new RequestConfirmDiscardJson
        {
            Items =
            [
                new RequestConfirmDiscardItemJson
                {
                    Material = SharedEnums.ElectronicMaterial.Battery,
                    Quantity = 2,
                    ApproximateWeightKg = 0.500m,
                },
                new RequestConfirmDiscardItemJson
                {
                    Material = SharedEnums.ElectronicMaterial.Notebook,
                    Quantity = 1,
                    ApproximateWeightKg = 1.500m,
                },
            ],
        };

        var response = await authClient.PostAsJsonAsync(
            $"api/v1/discards/{discard.Id}/confirm",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DiscardItems.CountAsync(value => value.DiscardId == discard.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Post_ShouldAllowOnlyOneTerminalTransition_WhenConfirmAndRejectRace()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(responsibleId);
        var discard = await CreatePendingDiscardAsync(
            responsibleId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery);
        using var confirmClient = CreateAuthenticatedClient(jwt);
        using var rejectClient = CreateAuthenticatedClient(jwt);
        var request = ValidRequest(SharedEnums.ElectronicMaterial.Battery);

        var responses = await Task.WhenAll(
            confirmClient.PostAsJsonAsync($"api/v1/discards/{discard.Id}/confirm", request),
            rejectClient.PostAsync($"api/v1/discards/{discard.Id}/reject", content: null));

        responses.Count(value => value.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.Count(value => value.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Discards.SingleAsync(value => value.Id == discard.Id);
        var requests = await db.CreditScoreRequests.CountAsync(
            value => value.DiscardId == discard.Id);
        requests.ShouldBe(persisted.Status == ApiEnums.DiscardStatus.Confirmed ? 1 : 0);
    }

    private static RequestConfirmDiscardJson ValidRequest(
        SharedEnums.ElectronicMaterial material) =>
        new()
        {
            Items =
            [
                new RequestConfirmDiscardItemJson
                {
                    Material = material,
                    Quantity = 3,
                    ApproximateWeightKg = 0.750m,
                },
            ],
        };
}

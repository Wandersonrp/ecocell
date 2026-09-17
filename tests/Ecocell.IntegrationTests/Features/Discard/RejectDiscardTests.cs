using System.Net;
using Ecocell.Api.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;

namespace Ecocell.IntegrationTests.Features.Discard;

public sealed class RejectDiscardTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Post_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await Client.PostAsync(
            $"api/v1/discards/{Guid.NewGuid()}/reject",
            content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn204AndPersistRejectedWithoutCreditRequest_WhenDiscardIsPending()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(responsibleId);
        var discard = await CreatePendingDiscardAsync(
            responsibleId,
            point.Id,
            ApiEnums.ElectronicMaterial.Battery);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsync(
            $"api/v1/discards/{discard.Id}/reject",
            content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Discards.SingleAsync(value => value.Id == discard.Id))
            .Status.ShouldBe(ApiEnums.DiscardStatus.Rejected);
        (await db.CreditScoreRequests.CountAsync(
            value => value.DiscardId == discard.Id)).ShouldBe(0);
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

        var response = await authClient.PostAsync(
            $"api/v1/discards/{discard.Id}/reject",
            content: null);

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
            var tracked = await db.Discards
                .Include(value => value.Items)
                .SingleAsync(value => value.Id == discard.Id);
            tracked.Confirm(tracked.Items.ToArray());
            await db.SaveChangesAsync();
        }
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsync(
            $"api/v1/discards/{discard.Id}/reject",
            content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}

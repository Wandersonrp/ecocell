using System.Net;
using Ecocell.Api.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Admin;

public class BlockPartnerTests : IntegrationTestBase
{
    public BlockPartnerTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn200_WhenCallerIsAdmin()
    {
        // Arrange — create pending partner, approve it first, then block
        var (_, adminJwt) = await CreateAdminAndLoginAsync();
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var responsible = await db.NaturalPeople.FirstAsync(p => p.Email == email);

        var legalPerson = await CreatePendingLegalPersonAsync(responsible.Id);
        using var authClient = CreateAuthenticatedClient(adminJwt);

        // Approve first (PendingApproval → Active)
        var approveResponse = await authClient.PostAsync(
            $"api/v1/admin/partners/{legalPerson.Id}/approve", null);
        approveResponse.IsSuccessStatusCode.ShouldBeTrue();

        // Act — block the now-active partner
        var response = await authClient.PostAsync(
            $"api/v1/admin/partners/{legalPerson.Id}/block", null);

        // Assert — endpoint returns 200 OK on success
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ShouldReturn403_WhenCallerIsNotAdmin()
    {
        // Arrange
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsync(
            $"api/v1/admin/partners/{Guid.NewGuid()}/block", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

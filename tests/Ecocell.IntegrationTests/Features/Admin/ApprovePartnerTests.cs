using System.Net;
using Ecocell.Api.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Admin;

public class ApprovePartnerTests : IntegrationTestBase
{
    public ApprovePartnerTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn204_WhenCallerIsAdmin()
    {
        // Arrange
        var (_, adminJwt) = await CreateAdminAndLoginAsync();
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var responsible = await db.NaturalPeople.FirstAsync(p => p.Email == email);

        var legalPerson = await CreatePendingLegalPersonAsync(responsible.Id);
        using var authClient = CreateAuthenticatedClient(adminJwt);

        // Act
        var response = await authClient.PostAsync(
            $"api/v1/admin/partners/{legalPerson.Id}/approve", null);

        // Assert — endpoint returns 204 No Content on success
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_ShouldReturn403_WhenCallerIsNotAdmin()
    {
        // Arrange
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsync(
            $"api/v1/admin/partners/{Guid.NewGuid()}/approve", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Shared.Requests.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Admin;

public class RejectPartnerTests : IntegrationTestBase
{
    public RejectPartnerTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn200_WhenCallerIsAdmin()
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
        var response = await authClient.PostAsJsonAsync(
            $"api/v1/admin/partners/{legalPerson.Id}/reject",
            new RequestRejectPartner { Reason = "Documentação incompleta." });

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
        var response = await authClient.PostAsJsonAsync(
            $"api/v1/admin/partners/{Guid.NewGuid()}/reject",
            new RequestRejectPartner { Reason = "motivo qualquer" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

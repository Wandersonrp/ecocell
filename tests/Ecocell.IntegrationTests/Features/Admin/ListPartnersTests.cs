using System.Net;
using System.Net.Http.Json;
using Ecocell.Shared.Responses;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Admin;

public class ListPartnersTests : IntegrationTestBase
{
    public ListPartnersTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ShouldReturn200WithList_WhenCallerIsAdmin()
    {
        // Arrange
        var (_, adminJwt) = await CreateAdminAndLoginAsync();
        using var authClient = CreateAuthenticatedClient(adminJwt);

        // Act
        var response = await authClient.GetAsync("api/v1/admin/partners");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResponsePartnerList>();
        body.ShouldNotBeNull();
        body.Items.ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenCallerIsNotAdmin()
    {
        // Arrange — regular user (role User)
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync("api/v1/admin/partners");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Enums;
using Ecocell.Shared.Responses;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.CollectorPoint;

[Collection(nameof(IntegrationTestCollection))]
public class GeneratePcQrCodeTests : IntegrationTestBase
{
    public GeneratePcQrCodeTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNoToken()
    {
        // Act
        var response = await Client.GetAsync($"api/collector-points/{Guid.NewGuid()}/qr");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenCallerOwnsActivePc()
    {
        // Arrange
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var pc = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync($"api/collector-points/{pc.Id}/qr");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseCollectorPointQrCode>();
        body.ShouldNotBeNull();
        body!.Qr.ShouldBe($"ecocell://pc/{pc.Id}");
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenPcIsMissing()
    {
        // Arrange
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync($"api/collector-points/{Guid.NewGuid()}/qr");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenPcBelongsToAnotherResponsible()
    {
        // Arrange — ponto ativo de OUTRO responsável (anti-IDOR).
        var (otherEmail, _) = await CreateAndLoginNaturalPersonAsync();
        var otherResponsibleId = await GetPersonIdByEmailAsync(otherEmail);
        var otherPc = await CreateActiveCollectorPointAsync(otherResponsibleId);

        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync($"api/collector-points/{otherPc.Id}/qr");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturn409_WhenPcIsNotActive()
    {
        // Arrange
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var pc = await CreatePendingLegalPersonAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync($"api/collector-points/{pc.Id}/qr");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Support)]
    public async Task Get_ShouldReturn403_WhenCallerIsAdminOrSupport(Role role)
    {
        // Arrange — usuário privilegiado é dono do PC ativo; Role != User barra no check de role.
        var (email, jwt) = await CreatePrivilegedUserAndLoginAsync(role);
        var userId = await GetPersonIdByEmailAsync(email);
        var pc = await CreateActiveCollectorPointAsync(userId);
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync($"api/collector-points/{pc.Id}/qr");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

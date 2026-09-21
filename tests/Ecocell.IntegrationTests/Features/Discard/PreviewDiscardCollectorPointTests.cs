using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Responses.Discards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features.Discard;

[Collection(nameof(IntegrationTestCollection))]
public class PreviewDiscardCollectorPointTests : IntegrationTestBase
{
    public PreviewDiscardCollectorPointTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await Client.GetAsync($"api/v1/discards/preview?qrCode=ecocell://pc/{Guid.NewGuid():D}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturn400_WhenQrCodeIsInvalid()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var client = CreateAuthenticatedClient(jwt);

        var response = await client.GetAsync("api/v1/discards/preview?qrCode=invalid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenPersonIsNotDepositor()
    {
        var (_, jwt) = await CreateAdminAndLoginAsync();
        using var client = CreateAuthenticatedClient(jwt);

        var response = await client.GetAsync($"api/v1/discards/preview?qrCode=ecocell://pc/{Guid.NewGuid():D}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenCollectorPointDoesNotExist()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var client = CreateAuthenticatedClient(jwt);

        var response = await client.GetAsync($"api/v1/discards/preview?qrCode=ecocell://pc/{Guid.NewGuid():D}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturn409_WhenCollectorPointIsInactive()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var point = await CreatePendingLegalPersonAsync(await GetPersonIdByEmailAsync(email));
        using var client = CreateAuthenticatedClient(jwt);

        var response = await client.GetAsync($"api/v1/discards/preview?qrCode=ecocell://pc/{point.Id:D}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_ShouldReturn200WithEmptyMaterials_WhenNoRuleIsActive()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var point = await CreateActiveCollectorPointAsync(await GetPersonIdByEmailAsync(email));
        using var client = CreateAuthenticatedClient(jwt);

        var response = await client.GetAsync($"api/v1/discards/preview?qrCode=ecocell://pc/{point.Id:D}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseDiscardPreviewJson>();
        body.ShouldNotBeNull();
        body!.AcceptedMaterials.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_ShouldReturn200WithPointAndActiveMaterials_WhenRequestIsValid()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var point = await CreateActiveCollectorPointAsync(await GetPersonIdByEmailAsync(email));
        await AddRuleAsync(point.Id, ApiEnums.ElectronicMaterial.Notebook, DateTime.UtcNow.AddDays(-1));
        await AddRuleAsync(point.Id, ApiEnums.ElectronicMaterial.Battery, DateTime.UtcNow.AddDays(-1));
        await AddRuleAsync(point.Id, ApiEnums.ElectronicMaterial.CellPhone, DateTime.UtcNow.AddDays(1));
        using var client = CreateAuthenticatedClient(jwt);

        var response = await client.GetAsync($"api/v1/discards/preview?qrCode=ecocell://pc/{point.Id:D}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseDiscardPreviewJson>();
        body.ShouldNotBeNull();
        body!.TradeName.ShouldBe(point.TradeName);
        body.FormattedAddress.ShouldNotBeEmpty();
        body.AcceptedMaterials.ShouldBe([
            SharedEnums.ElectronicMaterial.Battery,
            SharedEnums.ElectronicMaterial.Notebook,
        ]);
    }

    private async Task AddRuleAsync(Guid pointId, ApiEnums.ElectronicMaterial material, DateTime validFrom)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MaterialScoreRules.Add(new MaterialScoreRule(
            pointId, material, 10m, ApiEnums.MaterialScoreUnit.PerUnit, validFrom));
        await db.SaveChangesAsync();
    }
}

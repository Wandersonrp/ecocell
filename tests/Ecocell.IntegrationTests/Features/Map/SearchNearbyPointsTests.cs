using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Shared.Responses;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Map;

[Collection(nameof(IntegrationTestCollection))]
public class SearchNearbyPointsTests : IntegrationTestBase
{
    public SearchNearbyPointsTests(IntegrationTestFixture fixture) : base(fixture) { }

    private async Task SeedCollectPointAsync(string city, decimal lat, decimal lng)
    {
        var address = new Address("Rua A", "100", "Centro", city, "SP", "01000000", latitude: lat, longitude: lng);
        var legalPerson = new LegalPerson("Razão LTDA", $"EcoPonto {city}", "12345678000199", $"{city}@teste.com", Journey.CollectPoint, addressId: address.Id);
        legalPerson.Approve(Role.Admin);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Addresses.Add(address);
        db.People.Add(legalPerson);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNoToken()
    {
        var response = await Client.GetAsync("api/map/nearby?city=Campinas");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenCitySearchHasResults()
    {
        // Arrange
        await SeedCollectPointAsync("Campinas", -22.9m, -47.06m);
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync("api/map/nearby?city=Campinas");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var points = await response.Content.ReadFromJsonAsync<List<ResponseNearbyPoint>>();
        points.ShouldNotBeNull();
        points.ShouldHaveSingleItem();
        points![0].City.ShouldBe("Campinas");
    }

    [Fact]
    public async Task Get_ShouldReturn400_WhenNoSearchModeProvided()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.GetAsync("api/map/nearby");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ShouldReturnOrderedWithinRadius_WhenProximitySearch()
    {
        // Arrange — referência em São Paulo (-23.55, -46.63)
        await SeedCollectPointAsync("São Paulo", -23.55m, -46.63m);   // ~0 km
        await SeedCollectPointAsync("Guarulhos", -23.46m, -46.53m);   // ~14 km (fora de 10km)
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync("api/map/nearby?latitude=-23.55&longitude=-46.63&radiusKm=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var points = await response.Content.ReadFromJsonAsync<List<ResponseNearbyPoint>>();
        points.ShouldNotBeNull();
        points.ShouldHaveSingleItem();
        points![0].City.ShouldBe("São Paulo");
        points[0].DistanceKm.ShouldNotBeNull();
        points[0].DistanceKm!.Value.ShouldBeLessThan(1);
    }
}

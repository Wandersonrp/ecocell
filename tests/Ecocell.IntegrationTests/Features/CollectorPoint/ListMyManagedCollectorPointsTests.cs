using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;

namespace Ecocell.IntegrationTests.Features.CollectorPoint;

public class ListMyManagedCollectorPointsTests : IntegrationTestBase
{
    public ListMyManagedCollectorPointsTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNoToken()
    {
        // Act
        var response = await Client.GetAsync("api/collector-points/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturn200WithSeededPoint_WhenResponsibleHasCollectPoint()
    {
        // Arrange
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        Guid responsibleId;
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            responsibleId = await db.People.Where(p => p.Email == email).Select(p => p.Id).FirstAsync();
            db.LegalPeople.Add(new LegalPerson(
                "Recicla Verde LTDA", "Recicla Verde", "11222333000181",
                "contato@reciclaverde.com", ApiEnums.Journey.CollectPoint,
                responsiblePersonId: responsibleId));
            await db.SaveChangesAsync();
        }
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync("api/collector-points/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseManagedCollectorPointList>();
        body.ShouldNotBeNull();
        body.Items.Count.ShouldBe(1);
        body.Items[0].TradeName.ShouldBe("Recicla Verde");
    }

    [Fact]
    public async Task Get_ShouldExcludeOtherPersonsPoints_WhenTheyBelongToAnotherResponsible()
    {
        // Arrange
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var faker = new Faker("pt_BR");
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var otherResponsible = new NaturalPerson(
                faker.Name.FullName(),
                faker.Person.Cpf(includeFormatSymbols: false),
                DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
                ApiEnums.Role.User,
                faker.Internet.Email(),
                ApiEnums.Journey.Depositor);
            db.NaturalPeople.Add(otherResponsible);
            await db.SaveChangesAsync();

            db.LegalPeople.Add(new LegalPerson(
                "Outro PC LTDA", "Outro PC", "99888777000166",
                "outro@pc.com", ApiEnums.Journey.CollectPoint,
                responsiblePersonId: otherResponsible.Id));
            await db.SaveChangesAsync();
        }
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.GetAsync("api/collector-points/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseManagedCollectorPointList>();
        body.ShouldNotBeNull();
        body.Items.ShouldBeEmpty();
    }
}

using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Responses.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;

namespace Ecocell.IntegrationTests.Features.Ranking;

public sealed class GetRankingTests : IntegrationTestBase
{
    public GetRankingTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenRequestIsAnonymous()
    {
        var response = await Client.GetAsync("api/v1/rankings?scope=National");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenUserIsNotActiveDepositor()
    {
        var (_, jwt) = await CreateAdminAndLoginAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.GetAsync("api/v1/rankings?scope=National");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenDepositorWasSuspendedAfterLogin()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var person = await db.NaturalPeople.SingleAsync(value => value.Email == email);
            person.Block(ApiEnums.Role.Admin);
            await db.SaveChangesAsync();
        }

        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.GetAsync("api/v1/rankings?scope=National");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ShouldReturnNationalPageAndCurrentUserOutsidePage()
    {
        await CreateDepositorAsync("Ana Ativa", 30m);
        await CreateDepositorAsync("Bia Ativa", 30m);
        var (_, jwt, _) = await CreateAuthenticatedDepositorAsync("Caio da Silva", 10m);
        using var authClient = CreateAuthenticatedClient(jwt);

        var firstResponse = await authClient.GetAsync("api/v1/rankings?scope=National&page=1&pageSize=2");

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<ResponseRankingJson>();
        firstPage.ShouldNotBeNull();
        firstPage.Items.Select(item => item.Position).ShouldBe([1L, 1L]);
        firstPage.HasMore.ShouldBeTrue();
        firstPage.CurrentUser.ShouldNotBeNull();
        firstPage.CurrentUser.Position.ShouldBe(2L);
        firstPage.CurrentUser.ReducedName.ShouldBe("Caio S.");
        firstPage.CurrentUser.IsCurrentUser.ShouldBeTrue();
        firstJson.ShouldNotContain("depositorId");
        firstJson.ShouldNotContain("cpf");
        firstJson.ShouldNotContain("email");

        var secondResponse = await authClient.GetAsync("api/v1/rankings?scope=National&page=2&pageSize=2");

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<ResponseRankingJson>();
        secondPage.ShouldNotBeNull();
        secondPage.Items.ShouldHaveSingleItem();
        secondPage.Items[0].Position.ShouldBe(2L);
        secondPage.Items[0].IsCurrentUser.ShouldBeTrue();
        secondPage.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_ShouldNormalizeMunicipalPairAndKeepStatesSeparated()
    {
        var (_, jwt, currentUserId) = await CreateAuthenticatedDepositorAsync("Ana da Silva", 10m);
        var competitor = await CreateDepositorAsync("Bia Municipal", 20m);
        var otherState = await CreateDepositorAsync("Caio Municipal", 100m);
        await CreateCreditAsync(currentUserId, 10m, " São Paulo ", "sp");
        await CreateCreditAsync(competitor.Id, 20m, "SÃO PAULO", "SP");
        await CreateCreditAsync(otherState.Id, 100m, "São Paulo", "RJ");
        using var authClient = CreateAuthenticatedClient(jwt);

        var city = Uri.EscapeDataString(" SÃO PAULO ");
        var state = Uri.EscapeDataString(" sp ");
        var response = await authClient.GetAsync($"api/v1/rankings?scope=Municipal&city={city}&state={state}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseRankingJson>();
        body.ShouldNotBeNull();
        body.Items.Count.ShouldBe(2);
        body.Items.Select(item => item.ReducedName).ShouldBe(["Bia M.", "Ana S."]);
        body.Items.Select(item => item.Position).ShouldBe([1L, 2L]);
        body.CurrentUser.ShouldNotBeNull();
        body.CurrentUser.Position.ShouldBe(2L);
    }

    [Fact]
    public async Task Get_ShouldReturnNullCurrentUser_WhenUserHasNoScoreInMunicipality()
    {
        var (_, jwt, _) = await CreateAuthenticatedDepositorAsync("Ana Souza", 10m);
        var participant = await CreateDepositorAsync("Bia Lima", 20m);
        await CreateCreditAsync(participant.Id, 20m, "Betim", "MG");
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.GetAsync("api/v1/rankings?scope=Municipal&city=Betim&state=MG");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ResponseRankingJson>();
        body.ShouldNotBeNull();
        body.Items.ShouldHaveSingleItem();
        body.CurrentUser.ShouldBeNull();
    }

    [Fact]
    public async Task Get_ShouldReturn400_WhenMunicipalStateIsMissing()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.GetAsync("api/v1/rankings?scope=Municipal&city=Betim");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<(string email, string jwt, Guid id)> CreateAuthenticatedDepositorAsync(string fullName, decimal points)
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var person = await db.NaturalPeople.SingleAsync(value => value.Email == email);
        person.Update(fullName);
        db.DepositorTotalScores.Add(new DepositorTotalScore(person.Id, points));
        await db.SaveChangesAsync();
        return (email, jwt, person.Id);
    }

    private async Task<NaturalPerson> CreateDepositorAsync(string fullName, decimal totalPoints)
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(fullName, faker.Person.Cpf(includeFormatSymbols: false), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)), ApiEnums.Role.User, faker.Internet.Email(), ApiEnums.Journey.Depositor);
        person.Confirm();

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.People.Add(person);
        db.DepositorTotalScores.Add(new DepositorTotalScore(person.Id, totalPoints));
        await db.SaveChangesAsync();
        return person;
    }

    private async Task CreateCreditAsync(Guid depositorId, decimal points, string city, string state)
    {
        var faker = new Faker("pt_BR");
        var address = new Address("Rua Teste", "1", "Centro", city, state, "01001000");
        var point = new LegalPerson(faker.Company.CompanyName(), faker.Company.CompanyName(), faker.Company.Cnpj(includeFormatSymbols: false), faker.Internet.Email(), ApiEnums.Journey.CollectPoint, addressId: address.Id);
        point.Approve(ApiEnums.Role.Admin);
        var rule = new MaterialScoreRule(point.Id, ApiEnums.ElectronicMaterial.Battery, points, ApiEnums.MaterialScoreUnit.PerUnit, DateTime.UtcNow.AddDays(-1));
        var item = new DiscardItem(ApiEnums.ElectronicMaterial.Battery, 1, 1m, rule.Id);
        var discard = new Ecocell.Api.Entities.Discard(depositorId, point.Id, [item]);
        discard.Confirm([item]);
        var transaction = new DepositorScoreTransaction(discard.Id, depositorId, points);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Addresses.Add(address);
        db.People.Add(point);
        db.MaterialScoreRules.Add(rule);
        db.Discards.Add(discard);
        db.DepositorScoreTransactions.Add(transaction);
        await db.SaveChangesAsync();
    }
}

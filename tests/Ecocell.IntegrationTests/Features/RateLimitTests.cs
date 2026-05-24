using System.Net;
using System.Net.Http.Json;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Shared.Requests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features;

[Collection(nameof(IntegrationTestCollection))]
public class RateLimitTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public RateLimitTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = _fixture.CreateRateLimitedFactory(permitLimit: 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Post_ShouldReturn429_WhenRateLimitExceeded()
    {
        // Arrange
        // Bogus.Person é cached por Faker — instanciar Person novo por request
        // garante CPF/e-mail únicos e evita 409 Conflict antes do rate limit disparar.
        static RequestRegisterNaturalPerson BuildRequest()
        {
            var person = new Bogus.Person("pt_BR");
            return new RequestRegisterNaturalPerson
            {
                FullName = person.FullName,
                Email = person.Email,
                Cpf = person.Cpf(includeFormatSymbols: false),
                Journey = SharedEnums.Journey.Depositor,
                BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20))
            };
        }

        // Act — 2 requests dentro do limite (PermitLimit=2), 3º rejeitado
        var first = await _client.PostAsJsonAsync("api/natural-person", BuildRequest());
        var second = await _client.PostAsJsonAsync("api/natural-person", BuildRequest());
        var third = await _client.PostAsJsonAsync("api/natural-person", BuildRequest());

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        var body = await third.Content.ReadAsStringAsync();
        body.ShouldContain("messages");
        body.ShouldContain("Limite de requisi");
    }
}
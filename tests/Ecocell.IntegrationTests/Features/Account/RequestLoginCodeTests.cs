using System.Net;
using System.Net.Http.Json;
using Bogus;
using Ecocell.Shared.Requests;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Account;

public class RequestLoginCodeTests : IntegrationTestBase
{
    public RequestLoginCodeTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn202_WhenAccountExists()
    {
        // Arrange — active account
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/login/request-code",
            new RequestRequestLoginCode { Email = email });

        // Assert — 202 regardless of account existence (anti-enumeration)
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Post_ShouldReturn202_WhenAccountDoesNotExist()
    {
        // Arrange — unknown email
        var unknownEmail = new Faker().Internet.Email();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/login/request-code",
            new RequestRequestLoginCode { Email = unknownEmail });

        // Assert — 202 to prevent account enumeration
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }
}

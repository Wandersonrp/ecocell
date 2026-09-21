using System.Net;
using System.Net.Http.Json;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Account;

public class RefreshAccessTokenTests : IntegrationTestBase
{
    public RefreshAccessTokenTests(IntegrationTestFixture fixture) : base(fixture) { }

    private async Task<ResponseLogin> GetFreshTokensAsync()
    {
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        await Client.PostAsJsonAsync("api/account/login/request-code",
            new RequestRequestLoginCode { Email = email });

        var code = GetCapturingEmailSender().GetCapturedCode(email);

        var loginResponse = await Client.PostAsJsonAsync("api/account/login",
            new RequestVerifyLoginCode { Email = email, Code = code! });

        return (await loginResponse.Content.ReadFromJsonAsync<ResponseLogin>())!;
    }

    [Fact]
    public async Task Post_ShouldReturn200WithNewTokens_WhenRefreshTokenIsValid()
    {
        // Arrange
        var tokens = await GetFreshTokensAsync();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/refresh-token",
            new RequestRefreshAccessToken { RefreshToken = tokens.RefreshToken });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var newTokens = await response.Content.ReadFromJsonAsync<ResponseLogin>();
        newTokens.ShouldNotBeNull();
        newTokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        newTokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        newTokens.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Post_ShouldReturnError_WhenRefreshTokenIsUsedTwice()
    {
        // Arrange — obtain tokens and use refresh once
        var tokens = await GetFreshTokensAsync();

        var firstRefresh = await Client.PostAsJsonAsync("api/account/refresh-token",
            new RequestRefreshAccessToken { RefreshToken = tokens.RefreshToken });
        firstRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act — try same refresh token again (now invalidated)
        var secondRefresh = await Client.PostAsJsonAsync("api/account/refresh-token",
            new RequestRefreshAccessToken { RefreshToken = tokens.RefreshToken });

        // Assert
        secondRefresh.IsSuccessStatusCode.ShouldBeFalse();
    }
}

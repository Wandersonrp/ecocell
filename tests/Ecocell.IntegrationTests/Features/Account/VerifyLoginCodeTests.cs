using System.Net;
using System.Net.Http.Json;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Account;

public class VerifyLoginCodeTests : IntegrationTestBase
{
    public VerifyLoginCodeTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn200WithTokens_WhenCodeIsValid()
    {
        // Arrange — active account + OTP requested
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        await Client.PostAsJsonAsync("api/account/login/request-code",
            new RequestRequestLoginCode { Email = email });

        var code = GetCapturingEmailSender().GetCapturedCode(email);
        code.ShouldNotBeNull();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/login",
            new RequestVerifyLoginCode { Email = email, Code = code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResponseLogin>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        body.TokenType.ShouldBe("Bearer");
    }

    [Fact]
    public async Task Post_ShouldReturnError_WhenCodeIsInvalid()
    {
        // Arrange — active account
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        await Client.PostAsJsonAsync("api/account/login/request-code",
            new RequestRequestLoginCode { Email = email });

        // Act — wrong code
        var response = await Client.PostAsJsonAsync("api/account/login",
            new RequestVerifyLoginCode { Email = email, Code = "000000" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }
}

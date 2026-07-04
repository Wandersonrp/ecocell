using System.Text;
using Ecocell.Shared.Auth;
using Ecocell.Shared.Enums;
using Shouldly;

namespace Ecocell.UnitTests.Shared.Auth;

public class JwtClaimsReaderTests
{
    [Fact]
    public void GetJourney_ShouldReturnDepositor_WhenTokenHasDepositorJourney()
    {
        // Arrange
        var token = BuildToken("{\"journey\":\"Depositor\"}");

        // Act
        var journey = JwtClaimsReader.GetJourney(token);

        // Assert
        journey.ShouldBe(Journey.Depositor);
    }

    [Theory]
    [InlineData("CollectPoint", Journey.CollectPoint)]
    [InlineData("Collector", Journey.Collector)]
    public void GetJourney_ShouldReturnMatchingJourney_WhenTokenHasJourneyClaim(string claim, Journey expected)
    {
        // Arrange
        var token = BuildToken($"{{\"journey\":\"{claim}\"}}");

        // Act
        var journey = JwtClaimsReader.GetJourney(token);

        // Assert
        journey.ShouldBe(expected);
    }

    [Fact]
    public void GetJourney_ShouldReturnNull_WhenJourneyClaimIsMissing()
    {
        // Arrange
        var token = BuildToken("{\"email\":\"user@ecocell.com\"}");

        // Act
        var journey = JwtClaimsReader.GetJourney(token);

        // Assert
        journey.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("header.payload")]
    public void GetJourney_ShouldReturnNull_WhenTokenIsMalformed(string? token)
    {
        // Act
        var journey = JwtClaimsReader.GetJourney(token);

        // Assert
        journey.ShouldBeNull();
    }

    private static string BuildToken(string payloadJson)
    {
        var header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64Url(payloadJson);
        return $"{header}.{payload}.signature";
    }

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

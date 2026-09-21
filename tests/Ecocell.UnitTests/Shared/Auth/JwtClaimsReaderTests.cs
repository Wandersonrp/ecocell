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

    [Fact]
    public void GetRole_ShouldReturnAdmin_WhenTokenHasAdminRole()
    {
        // Arrange
        var token = BuildToken("{\"role\":\"Admin\"}");

        // Act
        var role = JwtClaimsReader.GetRole(token);

        // Assert
        role.ShouldBe(Role.Admin);
    }

    [Theory]
    [InlineData("Support", Role.Support)]
    [InlineData("User", Role.User)]
    public void GetRole_ShouldReturnMatchingRole_WhenTokenHasRoleClaim(string claim, Role expected)
    {
        // Arrange
        var token = BuildToken($"{{\"role\":\"{claim}\"}}");

        // Act
        var role = JwtClaimsReader.GetRole(token);

        // Assert
        role.ShouldBe(expected);
    }

    [Fact]
    public void GetRole_ShouldReturnNull_WhenRoleClaimIsMissing()
    {
        // Arrange
        var token = BuildToken("{\"email\":\"user@ecocell.com\"}");

        // Act
        var role = JwtClaimsReader.GetRole(token);

        // Assert
        role.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("header.payload")]
    public void GetRole_ShouldReturnNull_WhenTokenIsMalformed(string? token)
    {
        // Act
        var role = JwtClaimsReader.GetRole(token);

        // Assert
        role.ShouldBeNull();
    }

    [Fact]
    public void GetJourney_ShouldReturnNull_WhenPayloadIsInvalidBase64()
    {
        // "!!!" is not valid base64 → FormatException caught inside DecodePayload
        var journey = JwtClaimsReader.GetJourney("header.!!!invalid!!!.signature");

        journey.ShouldBeNull();
    }

    [Fact]
    public void GetJourney_ShouldReturnNull_WhenPayloadBase64DecodesTo2ByteJson()
    {
        // "{}" = 2 UTF-8 bytes → base64url = "e30" (3 chars, %4==3) → exercises "3 => =" padding branch
        var token = BuildToken("{}");

        var journey = JwtClaimsReader.GetJourney(token);

        journey.ShouldBeNull();
    }

    [Fact]
    public void GetJourney_ShouldReturnNull_WhenPayloadBase64DecodesTo1ByteFragment()
    {
        // "{" = 1 UTF-8 byte → base64url = "ew" (2 chars, %4==2) → exercises "2 => ==" padding branch
        // JsonDocument.Parse("{") throws JsonException → caught → returns null
        var token = BuildToken("{");

        var journey = JwtClaimsReader.GetJourney(token);

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

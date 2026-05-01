using System.Net;
using System.Text;
using Ecocell.Api.Configurations;
using Ecocell.Api.Services.External;
using Ecocell.Api.Shared;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shouldly;

namespace Ecocell.UnitTests.Services.External;

public class GeocodingServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly NominatimGeocodingService _service;
    private readonly GeocodingRequest _request = new("Avenida Paulista", "1578", "São Paulo", "SP", "01310100");

    public GeocodingServiceTests()
    {
        var http = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org")
        };
        var settings = Options.Create(new NominatimSettings
        {
            BaseUrl = "https://nominatim.openstreetmap.org",
            UserAgent = "test/1.0"
        });
        _service = new NominatimGeocodingService(http, settings);
    }

    private void SetupHttpResponse(HttpStatusCode status, string content)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task GeocodeAsync_ShouldReturnCoordinates_WhenAddressFound()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, """[{"lat":"-23.5505","lon":"-46.6333"}]""");

        // Act
        var result = await _service.GeocodeAsync(_request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Latitude.ShouldBe(-23.5505m);
        result.Value.Longitude.ShouldBe(-46.6333m);
    }

    [Fact]
    public async Task GeocodeAsync_ShouldReturnGeocodingNotFound_WhenArrayIsEmpty()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "[]");

        // Act
        var result = await _service.GeocodeAsync(_request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.GeocodingNotFound);
    }

    [Fact]
    public async Task GeocodeAsync_ShouldReturnGeocodingUnavailable_WhenHttpFails()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        // Act
        var result = await _service.GeocodeAsync(_request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.GeocodingUnavailable);
    }

    [Fact]
    public async Task GeocodeAsync_ShouldReturnGeocodingUnavailable_WhenRequestTimesOut()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("timeout"));

        // Act
        var result = await _service.GeocodeAsync(_request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.GeocodingUnavailable);
    }
}
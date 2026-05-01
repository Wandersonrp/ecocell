using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Ecocell.Api.Configurations;
using Ecocell.Api.Shared;
using Microsoft.Extensions.Options;

namespace Ecocell.Api.Services.External;

public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _http;

    public NominatimGeocodingService(HttpClient http, IOptions<NominatimSettings> settings)
    {
        _http = http;
    }

    public async Task<ResultT<GeocodingCoordinates>> GeocodeAsync(GeocodingRequest request, CancellationToken ct)
    {
        var query = $"/search" +
                    $"?street={Uri.EscapeDataString(request.Number + " " + request.Street)}" +
                    $"&city={Uri.EscapeDataString(request.City)}" +
                    $"&state={Uri.EscapeDataString(request.State)}" +
                    $"&postalcode={Uri.EscapeDataString(request.ZipCode)}" +
                    $"&format=jsonv2&limit=1&countrycodes=br";

        try
        {
            var results = await _http.GetFromJsonAsync<NominatimResult[]>(query, ct);

            if (results is null || results.Length == 0)
            {
                var formatted = $"{request.Street}, {request.Number}, {request.City}-{request.State}";
                return ResultT<GeocodingCoordinates>.Failure(Error.GeocodingNotFound(formatted));
            }

            var coords = new GeocodingCoordinates(
                decimal.Parse(results[0].Lat, CultureInfo.InvariantCulture),
                decimal.Parse(results[0].Lon, CultureInfo.InvariantCulture));

            return ResultT<GeocodingCoordinates>.Success(coords);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ResultT<GeocodingCoordinates>.Failure(
                new Error(ErrorCodes.GeocodingUnavailable, "Serviço de geocodificação indisponível"));
        }
    }

    private sealed record NominatimResult(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon);
}
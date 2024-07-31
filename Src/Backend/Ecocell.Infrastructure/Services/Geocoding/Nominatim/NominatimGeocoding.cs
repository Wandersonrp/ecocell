using Ecocell.Communication.Responses.Nominatim;
using Ecocell.Domain.Services.Geocoding;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Ecocell.Infrastructure.Services.Geocoding.Nominatim;

public class NominatimGeocoding : IGeocoding<ResponseNominatimGeocodingJson>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public NominatimGeocoding(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }
    public async Task<ResponseNominatimGeocodingJson?> GetGeocodingByPostalCode(string postalCode)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        var url = _configuration.GetValue<string>("NominatimApiUrl");

        try
        {
            var response = await httpClient.GetFromJsonAsync<object>($"/search.php?postalcode={postalCode}&format=json");

            if (response is null)
            {
                return null;
            }

            return (ResponseNominatimGeocodingJson)response;
        }
        catch
        {
            return null;
        }
    }
}

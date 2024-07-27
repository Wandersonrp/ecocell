using System.Text.Json.Serialization;

namespace Ecocell.Communication.Requests.Address;

public record RequestAddressJson
{
    [JsonPropertyName("street")]
    public string Street { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("complement")]
    public string? Complement { get; set; } = null!;

    [JsonPropertyName("neightborhood")]
    public string Neighborhood { get; set; } = string.Empty;

    [JsonPropertyName("zipcode")]
    public string ZipCode { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public string Latitude { get; set; } = string.Empty;

    [JsonPropertyName("longitude")]
    public string Longitude { get; set; } = string.Empty;
}

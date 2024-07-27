using System.Text.Json.Serialization;

namespace Ecocell.Communication.Responses.Nominatim;

public record ResponseNominatimGeocodingJson
{
    public IEnumerable<Root?> Roots { get; set; } = [];    
}

public record Root
{
    [JsonPropertyName("place_id")]
    public int PlaceId { get; set; }

    [JsonPropertyName("licence")]
    public string Licence { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public string Latitude { get; set; } = string.Empty;

    [JsonPropertyName("lon")]
    public string Longitude { get; set; } = string.Empty;

    [JsonPropertyName("class")]
    public string Class { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("importance")]
    public double Importance { get; set; }

    [JsonPropertyName("addresstype")]
    public string AddressType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("boundingbox")]
    public string[] BoundingBox { get; set; } = [];
}

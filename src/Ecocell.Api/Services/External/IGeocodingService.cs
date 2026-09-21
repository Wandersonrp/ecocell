using Ecocell.Api.Shared;

namespace Ecocell.Api.Services.External;

public record GeocodingRequest(string Street, string Number, string City, string State, string ZipCode);

public record GeocodingCoordinates(decimal Latitude, decimal Longitude);

public interface IGeocodingService
{
    Task<ResultT<GeocodingCoordinates>> GeocodeAsync(GeocodingRequest request, CancellationToken ct);
}
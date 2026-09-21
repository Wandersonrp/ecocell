using Microsoft.Extensions.Logging;

namespace Ecocell.Mobile.Services.Location;

public class LocationService : ILocationService
{
    private readonly ILogger<LocationService> _logger;

    public LocationService(ILogger<LocationService> logger)
    {
        _logger = logger;
    }

    public async Task<EcoLocation?> GetCurrentAsync(CancellationToken ct = default)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                return null;

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request, ct)
                ?? await Geolocation.Default.GetLastKnownLocationAsync();

            return location is null ? null : new EcoLocation(location.Latitude, location.Longitude);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter localização; fallback para busca por cidade.");
            return null;
        }
    }
}

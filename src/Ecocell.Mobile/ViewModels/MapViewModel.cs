using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Location;
using Ecocell.Shared.Responses;

namespace Ecocell.Mobile.ViewModels;

/// <summary>Orquestra a tela de mapa: decide entre busca por GPS ou por cidade e mantém o estado da UI.</summary>
public class MapViewModel
{
    private readonly IMapClient _mapClient;
    private readonly ILocationService _locationService;

    public MapViewModel(IMapClient mapClient, ILocationService locationService)
    {
        _mapClient = mapClient;
        _locationService = locationService;
    }

    public const double DefaultRadiusKm = 10;

    private const string PrefKeyLatitude = "map.last_center_latitude";
    private const string PrefKeyLongitude = "map.last_center_longitude";

    public IReadOnlyList<ResponseNearbyPoint> Points { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool LocationDenied { get; private set; }
    public EcoLocation? Center { get; private set; }
    public ResponseNearbyPoint? Selected { get; private set; }

    /// <summary>
    /// Último centro persistido no dispositivo (Preferences). Usado como centro inicial do mapa
    /// ao reabrir a tela, evitando o "salto" do fallback padrão até o GPS resolver (WND-273).
    /// </summary>
    public EcoLocation? CachedCenter =>
        Preferences.ContainsKey(PrefKeyLatitude) && Preferences.ContainsKey(PrefKeyLongitude)
            ? new EcoLocation(Preferences.Get(PrefKeyLatitude, 0d), Preferences.Get(PrefKeyLongitude, 0d))
            : null;

    public bool IsEmpty => !IsLoading && ErrorMessage is null && Points.Count == 0;

    /// <summary>Tenta GPS; se negado, sinaliza <see cref="LocationDenied"/> para a UI abrir a busca por cidade.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        LocationDenied = false;
        var location = await _locationService.GetCurrentAsync(ct);
        if (location is null)
        {
            LocationDenied = true;
            return;
        }

        Center = location;
        Preferences.Set(PrefKeyLatitude, location.Latitude);
        Preferences.Set(PrefKeyLongitude, location.Longitude);
        await LoadAsync(() => _mapClient.SearchNearbyAsync(location.Latitude, location.Longitude, DefaultRadiusKm, null, ct));
    }

    public async Task SearchByCityAsync(string city, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(city)) return;
        await LoadAsync(() => _mapClient.SearchNearbyAsync(null, null, null, city, ct));
    }

    public void Select(ResponseNearbyPoint? point) => Selected = point;

    private async Task LoadAsync(Func<Task<Refit.IApiResponse<List<ResponseNearbyPoint>>>> call)
    {
        IsLoading = true;
        ErrorMessage = null;
        Selected = null;
        Points = [];
        try
        {
            var response = await call();
            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                Points = response.Content;
            }
            else
            {
                ErrorMessage = "Não foi possível carregar os pontos de coleta.";
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Falha de conexão. Verifique sua internet e tente novamente.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

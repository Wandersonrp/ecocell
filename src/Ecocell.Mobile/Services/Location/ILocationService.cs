namespace Ecocell.Mobile.Services.Location;

public record EcoLocation(double Latitude, double Longitude);

public interface ILocationService
{
    /// <summary>Retorna a localização atual, ou <c>null</c> quando a permissão for negada ou o GPS indisponível.</summary>
    Task<EcoLocation?> GetCurrentAsync(CancellationToken ct = default);
}

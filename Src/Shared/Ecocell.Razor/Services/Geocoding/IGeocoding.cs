namespace Ecocell.Razor.Services.Geocoding;

public interface IGeocoding<T>
{
    Task<T> GetGeocodingByPostalCode(string postalCode);
}

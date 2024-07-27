namespace Ecocell.Domain.Services.Geocoding;

public interface IGeocoding<T>
{
    Task<T> GetGeocodingByPostalCode(string postalCode);
}

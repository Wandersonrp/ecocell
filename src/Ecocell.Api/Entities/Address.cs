namespace Ecocell.Api.Entities;

/// <summary>
/// Entidade que representa um endereço físico. Independente — referenciada por FK em
/// <see cref="LegalPerson"/> e, futuramente, em <see cref="NaturalPerson"/>.
/// Coordenadas (<see cref="Latitude"/> e <see cref="Longitude"/>) são preenchidas via
/// geocoding em US003.
/// </summary>
public class Address : BaseEntity
{
    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string Neighborhood { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string ZipCode { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    /// <summary>
    /// Cria um novo endereço com os campos obrigatórios. Lat/Lng ficam nulos até o
    /// geocoding ser executado em US003.
    /// </summary>
    public Address(
        string street,
        string number,
        string neighborhood,
        string city,
        string state,
        string zipCode,
        string? complement = null)
    {
        Street = street;
        Number = number;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        ZipCode = zipCode;
        Complement = complement;
    }
}
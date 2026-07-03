namespace Ecocell.Shared.Responses;

/// <summary>Ponto de coleta retornado pela busca espacial do mapa. <c>DistanceKm</c> é nulo no modo cidade.</summary>
public record ResponseNearbyPoint
{
    public Guid Id { get; init; }
    public string TradeName { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public double? DistanceKm { get; init; }
}

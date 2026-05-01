namespace Ecocell.Shared.Requests;

public record RequestUpdateLegalPersonCoordinates
{
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
}
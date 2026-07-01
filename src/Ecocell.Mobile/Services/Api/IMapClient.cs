using Refit;
using Ecocell.Shared.Responses;

namespace Ecocell.Mobile.Services.Api;

public interface IMapClient
{
    [Get("/api/map/nearby")]
    Task<IApiResponse<List<ResponseNearbyPoint>>> SearchNearbyAsync(
        [Query] double? latitude,
        [Query] double? longitude,
        [Query] double? radiusKm,
        [Query] string? city,
        CancellationToken ct = default);
}

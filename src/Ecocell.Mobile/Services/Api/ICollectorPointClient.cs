using Ecocell.Shared.Responses;
using Refit;

namespace Ecocell.Mobile.Services.Api;

public interface ICollectorPointClient
{
    [Get("/api/collector-points/me")]
    Task<IApiResponse<ResponseManagedCollectorPointList>> ListMineAsync(CancellationToken ct = default);
}

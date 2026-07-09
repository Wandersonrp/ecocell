using Ecocell.Shared.Responses;
using Refit;

namespace Ecocell.Mobile.Services.Api;

public interface ICollectorPointClient
{
    [Get("/api/collector-points/me")]
    Task<IApiResponse<ResponseManagedCollectorPointList>> ListMineAsync(CancellationToken ct = default);

    [Get("/api/collector-points/{id}/qr")]
    Task<IApiResponse<ResponseCollectorPointQrCode>> GetQrCodeAsync(Guid id, CancellationToken ct = default);
}

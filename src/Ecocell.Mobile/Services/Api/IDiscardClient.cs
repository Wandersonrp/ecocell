using Ecocell.Shared.Requests.Discards;
using Ecocell.Shared.Responses;
using Ecocell.Shared.Responses.Discards;
using Refit;

namespace Ecocell.Mobile.Services.Api;

public interface IDiscardClient
{
    [Get("/api/v1/discards/preview")]
    Task<IApiResponse<ResponseDiscardPreviewJson>> PreviewAsync(
        [Query] string qrCode,
        CancellationToken ct = default);

    [Post("/api/v1/discards")]
    Task<IApiResponse<ResponseRegisterDiscardJson>> RegisterAsync(
        [Body] RequestRegisterDiscardJson request,
        CancellationToken ct = default);

    [Get("/api/v1/collector-points/{collectorPointId}/discards/pending")]
    Task<IApiResponse<ResponsePendingDiscardListJson>> ListPendingAsync(
        Guid collectorPointId,
        CancellationToken ct = default);

    [Post("/api/v1/discards/{discardId}/confirm")]
    Task<IApiResponse> ConfirmAsync(
        Guid discardId,
        [Body] RequestConfirmDiscardJson request,
        CancellationToken ct = default);

    [Post("/api/v1/discards/{discardId}/reject")]
    Task<IApiResponse> RejectAsync(Guid discardId, CancellationToken ct = default);
}

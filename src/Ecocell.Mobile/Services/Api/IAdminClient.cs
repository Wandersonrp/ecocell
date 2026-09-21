using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests.Admin;
using Ecocell.Shared.Responses;
using Refit;

namespace Ecocell.Mobile.Services.Api;

public interface IAdminClient
{
    [Get("/api/v1/admin/partners")]
    Task<IApiResponse<ResponsePartnerList>> ListPartnersAsync(
        [Query] PersonStatus? status,
        [Query] Journey? journey,
        [Query] Guid? cursor,
        [Query] int? pageSize,
        CancellationToken ct = default);

    [Post("/api/v1/admin/partners/{id}/approve")]
    Task<IApiResponse> ApprovePartnerAsync(Guid id, CancellationToken ct = default);

    [Post("/api/v1/admin/partners/{id}/reject")]
    Task<IApiResponse> RejectPartnerAsync(Guid id, [Body] RequestRejectPartner body, CancellationToken ct = default);
}

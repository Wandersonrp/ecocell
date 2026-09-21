using Ecocell.Shared.Requests.Ranking;
using Ecocell.Shared.Responses.Ranking;
using Refit;

namespace Ecocell.Mobile.Services.Api;

public interface IRankingClient
{
    [Get("/api/v1/rankings")]
    Task<IApiResponse<ResponseRankingJson>> GetAsync(
        [Query] RequestGetRankingJson request,
        CancellationToken ct = default);
}

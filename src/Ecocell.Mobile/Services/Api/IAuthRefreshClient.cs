using Refit;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;

namespace Ecocell.Mobile.Services.Api;

/// <summary>
/// Client dedicado ao refresh de tokens. Registrado SEM AuthTokenHandler
/// para evitar ciclo de DI e recursão de refresh.
/// </summary>
public interface IAuthRefreshClient
{
    [Post("/api/account/refresh-token")]
    Task<IApiResponse<ResponseLogin>> RefreshAccessTokenAsync(
        [Body] RequestRefreshAccessToken request,
        CancellationToken ct = default);
}

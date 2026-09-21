using System.Net;
using System.Net.Http.Headers;
using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Http;
using Ecocell.Shared.Requests;

namespace Ecocell.Mobile.Services.Auth;

/// <summary>
/// Injeta o Bearer token nas requests autenticadas e, ao receber 401, renova o par
/// de tokens uma única vez via IAuthRefreshClient e repete a request. Falha no refresh
/// => SignOutAsync e propaga o 401. Não deve ser anexado a IAccountClient (endpoints
/// públicos; 401 ali = OTP inválido).
/// </summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private readonly AuthStateService _authState;
    private readonly IAuthRefreshClient _refreshClient;

    public AuthTokenHandler(AuthStateService authState, IAuthRefreshClient refreshClient)
    {
        _authState = authState;
        _refreshClient = refreshClient;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ApplyBearer(request);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var refreshed = await TryRefreshAsync(cancellationToken);
        if (!refreshed)
        {
            await _authState.SignOutAsync();
            return response;
        }

        response.Dispose();
        var retry = await HttpRequestCloner.CloneAsync(request);
        ApplyBearer(retry);
        return await base.SendAsync(retry, cancellationToken);
    }

    private void ApplyBearer(HttpRequestMessage request)
    {
        var token = _authState.CurrentAccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// Renova o par de tokens com no máximo um refresh concorrente (SemaphoreSlim).
    /// Se outra request já renovou enquanto aguardávamos o lock, reaproveita o estado.
    /// </summary>
    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = _authState.CurrentRefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_authState.CurrentRefreshToken != refreshToken)
            {
                return _authState.IsAuthenticated;
            }

            var result = await _refreshClient.RefreshAccessTokenAsync(
                new RequestRefreshAccessToken { RefreshToken = refreshToken }, cancellationToken);

            if (!result.IsSuccessStatusCode || result.Content is null)
            {
                return false;
            }

            await _authState.SignInAsync(result.Content);
            return true;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

}

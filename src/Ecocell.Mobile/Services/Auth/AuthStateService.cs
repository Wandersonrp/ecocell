using Ecocell.Shared.Responses;

namespace Ecocell.Mobile.Services.Auth;

/// <summary>
/// Mantém o estado de autenticação em memória (fonte síncrona para o handler)
/// respaldado pelo armazenamento seguro. Considera autenticado se o access token
/// é válido OU se ainda há refresh token válido para renovar.
/// </summary>
public sealed class AuthStateService
{
    private readonly ISecureTokenStore _store;
    private StoredTokens? _tokens;

    public AuthStateService(ISecureTokenStore store) => _store = store;

    public event Action? AuthStateChanged;

    public bool IsAuthenticated =>
        _tokens is not null
        && (_tokens.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow
            || _tokens.RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow);

    public string? CurrentAccessToken => _tokens?.AccessToken;

    public string? CurrentRefreshToken => _tokens?.RefreshToken;

    public async Task InitializeAsync() => _tokens = await _store.GetAsync();

    public async Task SignInAsync(ResponseLogin tokens)
    {
        await _store.SaveAsync(tokens);
        _tokens = await _store.GetAsync();
        AuthStateChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        await _store.ClearAsync();
        _tokens = null;
        AuthStateChanged?.Invoke();
    }
}

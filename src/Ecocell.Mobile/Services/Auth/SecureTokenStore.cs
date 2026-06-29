using System.Globalization;
using Ecocell.Shared.Responses;
using Microsoft.Maui.Storage;

namespace Ecocell.Mobile.Services.Auth;

/// <summary>
/// Persiste o par de tokens no armazenamento seguro da plataforma
/// (Keychain no iOS, KeyStore + EncryptedSharedPreferences no Android).
/// </summary>
public sealed class SecureTokenStore : ISecureTokenStore
{
    private const string AccessTokenKey = "auth_access_token";
    private const string AccessExpiresKey = "auth_access_expires_utc";
    private const string RefreshTokenKey = "auth_refresh_token";
    private const string RefreshExpiresKey = "auth_refresh_expires_utc";

    public async Task SaveAsync(ResponseLogin tokens)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, tokens.AccessToken);
        await SecureStorage.Default.SetAsync(AccessExpiresKey, tokens.ExpiresAtUtc.ToString("O"));
        await SecureStorage.Default.SetAsync(RefreshTokenKey, tokens.RefreshToken);
        await SecureStorage.Default.SetAsync(RefreshExpiresKey, tokens.RefreshTokenExpiresAtUtc.ToString("O"));
    }

    public async Task<StoredTokens?> GetAsync()
    {
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var accessExpires = await SecureStorage.Default.GetAsync(AccessExpiresKey);
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        var refreshExpires = await SecureStorage.Default.GetAsync(RefreshExpiresKey);

        if (string.IsNullOrEmpty(accessToken)
            || string.IsNullOrEmpty(accessExpires)
            || string.IsNullOrEmpty(refreshToken)
            || string.IsNullOrEmpty(refreshExpires))
        {
            return null;
        }

        return new StoredTokens(
            accessToken,
            DateTimeOffset.Parse(accessExpires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            refreshToken,
            DateTimeOffset.Parse(refreshExpires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(AccessExpiresKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(RefreshExpiresKey);
        return Task.CompletedTask;
    }
}

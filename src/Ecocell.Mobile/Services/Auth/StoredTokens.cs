namespace Ecocell.Mobile.Services.Auth;

/// <summary>Par de tokens persistido localmente, lido na inicialização do app.</summary>
public sealed record StoredTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

namespace Ecocell.Shared.Responses;

/// <summary>
/// Resposta retornada após autenticação bem-sucedida via código OTP.
/// </summary>
public sealed record ResponseLogin
{
    /// <summary>Token JWT de acesso.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Data e hora de expiração do token em UTC.</summary>
    public DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>Tipo do token. Sempre "Bearer".</summary>
    public string TokenType { get; init; } = "Bearer";
}

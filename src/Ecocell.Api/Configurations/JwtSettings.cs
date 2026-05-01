using System.ComponentModel.DataAnnotations;

namespace Ecocell.Api.Configurations;

/// <summary>
/// Configurações para geração e validação de tokens JWT.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Chave de assinatura HMAC-SHA256. Deve ter no mínimo 32 caracteres.
    /// </summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:SigningKey deve ter no mínimo 32 caracteres.")]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Emissor (iss) do token.</summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Audiência (aud) do token.</summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>Duração do access token em minutos. Padrão: 60.</summary>
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 60;

    /// <summary>Duração do refresh token em dias. Padrão: 7.</summary>
    [Range(1, 90)]
    public int RefreshTokenLifetimeDays { get; init; } = 7;
}

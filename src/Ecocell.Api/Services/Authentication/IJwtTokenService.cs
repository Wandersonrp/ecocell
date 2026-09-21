using Ecocell.Api.Entities;

namespace Ecocell.Api.Services.Authentication;

/// <summary>
/// Contrato para geração de tokens JWT de acesso e tokens opacos de renovação.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Gera um token JWT de acesso assinado para a pessoa autenticada.
    /// </summary>
    /// <param name="person">Entidade <see cref="Person"/> autenticada.</param>
    /// <returns><see cref="JwtToken"/> com o token assinado e a data de expiração.</returns>
    JwtToken Generate(Person person);

    /// <summary>
    /// Gera um token opaco de renovação criptograficamente aleatório (32 bytes, hex-encoded).
    /// O TTL é determinado por <c>JwtSettings.RefreshTokenLifetimeDays</c>.
    /// </summary>
    /// <returns><see cref="RefreshTokenResult"/> com o token e a data de expiração.</returns>
    RefreshTokenResult GenerateRefreshToken();
}

/// <summary>
/// Representa o token JWT gerado e sua data de expiração.
/// </summary>
/// <param name="AccessToken">Token JWT assinado.</param>
/// <param name="ExpiresAtUtc">Data e hora de expiração em UTC.</param>
public sealed record JwtToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Representa o token opaco de renovação e sua data de expiração.
/// </summary>
/// <param name="Token">Hex string de 64 caracteres (32 bytes de entropia).</param>
/// <param name="ExpiresAtUtc">Data e hora de expiração em UTC.</param>
public sealed record RefreshTokenResult(string Token, DateTimeOffset ExpiresAtUtc);

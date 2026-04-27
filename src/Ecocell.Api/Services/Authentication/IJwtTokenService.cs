using Ecocell.Api.Entities;

namespace Ecocell.Api.Services.Authentication;

/// <summary>
/// Contrato para geração de tokens JWT de acesso.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Gera um token JWT de acesso assinado para a pessoa autenticada.
    /// </summary>
    /// <param name="person">Entidade <see cref="Person"/> autenticada.</param>
    /// <returns><see cref="JwtToken"/> com o token assinado e a data de expiração.</returns>
    JwtToken Generate(Person person);
}

/// <summary>
/// Representa o token JWT gerado e sua data de expiração.
/// </summary>
/// <param name="AccessToken">Token JWT assinado.</param>
/// <param name="ExpiresAtUtc">Data e hora de expiração em UTC.</param>
public sealed record JwtToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

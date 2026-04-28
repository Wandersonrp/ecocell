using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecocell.Api.Configurations;
using Ecocell.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ecocell.Api.Services.Authentication;

/// <summary>
/// Implementação de <see cref="IJwtTokenService"/> que gera tokens JWT assinados com HMAC-SHA256.
/// Claims emitidas: sub, email, role, journey, person_type, jti, iat, exp.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    /// <summary>
    /// Inicializa o serviço com as configurações JWT injetadas.
    /// </summary>
    /// <param name="settings">Configurações JWT obtidas do sistema de opções.</param>
    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Gera um token JWT de acesso com as claims da pessoa autenticada.
    /// </summary>
    /// <param name="person">Entidade <see cref="Person"/> autenticada.</param>
    /// <returns><see cref="JwtToken"/> com o token assinado e a data de expiração em UTC.</returns>
    public JwtToken Generate(Person person)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_settings.AccessTokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sid, person.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, person.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("role", person.Role.ToString()),
            new Claim("journey", person.Journey.ToString()),
            new Claim("person_type", person.PersonType.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

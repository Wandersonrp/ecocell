using System.Text;
using System.Text.Json;
using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Auth;

/// <summary>
/// Lê claims de um access token JWT no lado do cliente <b>sem validar a assinatura</b>
/// (a validação é responsabilidade do servidor). Uso típico: decidir o roteamento
/// pós-login a partir da jornada do usuário.
/// </summary>
public static class JwtClaimsReader
{
    /// <summary>
    /// Extrai a claim <c>journey</c> do payload do token. Retorna <c>null</c> quando o
    /// token é nulo/malformado ou a claim está ausente ou não corresponde a uma jornada conhecida.
    /// </summary>
    public static Journey? GetJourney(string? accessToken)
    {
        var payload = DecodePayload(accessToken);

        if (payload is null || !payload.Value.TryGetProperty("journey", out var journeyClaim))
        {
            return null;
        }

        return Enum.TryParse<Journey>(journeyClaim.GetString(), out var journey) ? journey : null;
    }

    private static JsonElement? DecodePayload(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');

        if (parts.Length < 3)
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');

        base64 += (base64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };

        return Convert.FromBase64String(base64);
    }
}

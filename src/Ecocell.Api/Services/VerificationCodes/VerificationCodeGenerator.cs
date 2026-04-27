using System.Security.Cryptography;
using System.Text;

namespace Ecocell.Api.Services.VerificationCodes;

/// <summary>
/// Utilitários para geração e hash de códigos OTP de 6 dígitos.
/// </summary>
public static class VerificationCodeGenerator
{
    /// <summary>
    /// Gera um código OTP numérico de 6 dígitos com zero-pad usando <see cref="RandomNumberGenerator"/>.
    /// </summary>
    /// <returns>String de exatamente 6 dígitos (ex.: "042837").</returns>
    public static string Generate6Digit() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>
    /// Calcula o hash SHA-256 do código informado e retorna a representação hexadecimal em lowercase.
    /// </summary>
    /// <param name="code">Código OTP em texto plano.</param>
    /// <returns>Hash SHA-256 em hexadecimal lowercase (64 caracteres).</returns>
    public static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexStringLower(bytes);
    }
}

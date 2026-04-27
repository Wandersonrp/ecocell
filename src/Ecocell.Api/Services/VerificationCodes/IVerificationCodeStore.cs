namespace Ecocell.Api.Services.VerificationCodes;

/// <summary>
/// Contrato para armazenamento e recuperação de códigos OTP com controle de TTL e tentativas.
/// </summary>
public interface IVerificationCodeStore
{
    /// <summary>
    /// Persiste o hash do código OTP com o TTL e contador de tentativas zerado.
    /// </summary>
    /// <param name="key">Chave de identificação no store (use <see cref="BuildKey"/>).</param>
    /// <param name="codeHash">Hash SHA-256 do código gerado.</param>
    /// <param name="ttl">Tempo de vida do registro.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task SaveAsync(string key, string codeHash, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Recupera o registro OTP associado à chave; retorna <c>null</c> se inexistente ou expirado.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<VerificationCodeRecord?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Incrementa o contador de tentativas inválidas para a chave informada.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task IncrementAttemptsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Remove o registro OTP da chave informada (após confirmação bem-sucedida).
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Constrói a chave canônica usada no store a partir da finalidade e do identificador.
    /// </summary>
    /// <param name="purpose">Finalidade do código OTP.</param>
    /// <param name="identifier">Identificador do usuário (e-mail normalizado).</param>
    static string BuildKey(VerificationCodePurpose purpose, string identifier)
    {
        var prefix = purpose switch
        {
            VerificationCodePurpose.EmailConfirmation => "otp:confirm",
            VerificationCodePurpose.Login => "otp:login",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
        return $"{prefix}:{identifier.ToLowerInvariant().Trim()}";
    }
}

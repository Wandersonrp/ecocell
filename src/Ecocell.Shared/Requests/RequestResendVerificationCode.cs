namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição para reenvio do código OTP de confirmação de conta.
/// </summary>
public record RequestResendVerificationCode
{
    /// <summary>E-mail cadastrado na plataforma.</summary>
    public string Email { get; set; } = string.Empty;
}

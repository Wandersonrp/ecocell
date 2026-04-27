namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição para confirmação de conta via código OTP enviado ao e-mail.
/// </summary>
public record RequestConfirmAccount
{
    /// <summary>E-mail cadastrado na plataforma.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Código OTP de 6 dígitos recebido por e-mail.</summary>
    public string Code { get; set; } = string.Empty;
}

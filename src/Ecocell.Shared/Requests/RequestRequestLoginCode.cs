namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição para solicitar o envio de um código OTP de login ao e-mail cadastrado.
/// </summary>
public record RequestRequestLoginCode
{
    /// <summary>E-mail cadastrado na plataforma.</summary>
    public string Email { get; set; } = string.Empty;
}

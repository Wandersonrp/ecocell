namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição para verificar o código OTP de login e obter o token de acesso.
/// </summary>
public record RequestVerifyLoginCode
{
    /// <summary>E-mail cadastrado na plataforma.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Código OTP de 6 dígitos recebido por e-mail.</summary>
    public string Code { get; set; } = string.Empty;
}

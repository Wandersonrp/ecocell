namespace Ecocell.Api.Services.VerificationCodes;

/// <summary>
/// Finalidade do código de verificação OTP, usada para construir a chave no Redis.
/// </summary>
public enum VerificationCodePurpose
{
    /// <summary>Confirmação de e-mail após o cadastro.</summary>
    EmailConfirmation = 1,

    /// <summary>Autenticação passwordless no login (uso futuro).</summary>
    Login = 2
}

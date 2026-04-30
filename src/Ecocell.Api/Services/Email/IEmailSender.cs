namespace Ecocell.Api.Services.Email;

/// <summary>
/// Contrato para envio de e-mails transacionais da plataforma.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envia o código de verificação OTP para o endereço de e-mail informado.
    /// </summary>
    /// <param name="email">Destinatário do e-mail.</param>
    /// <param name="code">Código OTP de 6 dígitos a ser enviado.</param>
    /// <param name="ct">Token de cancelamento da operação.</param>
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);

    /// <summary>
    /// Envia notificação de cadastro recebido para a pessoa jurídica ou seu gestor.
    /// </summary>
    /// <param name="email">Destinatário da notificação.</param>
    /// <param name="ct">Token de cancelamento da operação.</param>
    Task SendLegalPersonRegistrationNotificationAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Envia e-mail de rejeição ao parceiro com o motivo informado pelo admin (RN008).
    /// </summary>
    Task SendPartnerRejectionAsync(string email, string reason, CancellationToken ct = default);
}

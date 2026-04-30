namespace Ecocell.Api.Services.Email;

/// <summary>
/// Implementação stub de <see cref="IEmailSender"/> que registra e-mails transacionais no log em vez de enviá-los por SMTP.
/// Deve ser substituída por uma implementação real (MailKit / SendGrid) em entrega futura.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registra o código de verificação no log estruturado (não realiza envio real de e-mail).
    /// </summary>
    /// <param name="email">Destinatário do e-mail.</param>
    /// <param name="code">Código OTP de 6 dígitos.</param>
    /// <param name="ct">Token de cancelamento da operação.</param>
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("[OTP] Código de verificação para {Email}: {Code}", email, code);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registra no log que a notificação de cadastro de pessoa jurídica foi enviada (não realiza envio real).
    /// </summary>
    /// <param name="email">Destinatário da notificação.</param>
    /// <param name="ct">Token de cancelamento da operação.</param>
    public Task SendLegalPersonRegistrationNotificationAsync(string email, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL] Notificação de cadastro de pessoa jurídica enviada para {Email}", email);
        return Task.CompletedTask;
    }

    public Task SendPartnerRejectionAsync(string email, string reason, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL] Rejeição de parceiro enviada para {Email}. Motivo: {Reason}", email, reason);
        return Task.CompletedTask;
    }
}

namespace Ecocell.Api.Services.Email;

/// <summary>
/// Implementação stub de <see cref="IEmailSender"/> que registra o código OTP no log em vez de enviá-lo por SMTP.
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
}

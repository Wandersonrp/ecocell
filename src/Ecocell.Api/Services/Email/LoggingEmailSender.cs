using Ecocell.Api.Enums;

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

    public Task SendAsync(
        string email,
        EmailType type,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL] Tipo={Type} → {Email} | Variáveis={@Variables}", type, email, variables);
        return Task.CompletedTask;
    }
}
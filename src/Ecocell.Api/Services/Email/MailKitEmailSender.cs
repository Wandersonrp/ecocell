using Ecocell.Api.Configurations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ecocell.Api.Services.Email;

/// <summary>
/// Implementação de <see cref="IEmailSender"/> que envia e-mails via SMTP usando MailKit.
/// Ativa em Production e Staging; em Development usa <see cref="LoggingEmailSender"/>.
/// </summary>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly MailSettings _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<MailSettings> settings, ILogger<MailKitEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
    {
        var message = BuildMessage(
            to: email,
            subject: "EcoCell — Seu código de verificação",
            body: $"<p>Seu código de verificação é: <strong>{code}</strong></p><p>Válido por 10 minutos.</p>");

        await SendAsync(message, ct);
        _logger.LogInformation("Código OTP enviado para {Email}", email);
    }

    public async Task SendLegalPersonRegistrationNotificationAsync(string email, CancellationToken ct = default)
    {
        var message = BuildMessage(
            to: email,
            subject: "EcoCell — Cadastro recebido",
            body: "<p>Seu cadastro foi recebido e está em análise. Em breve entraremos em contato.</p>");

        await SendAsync(message, ct);
        _logger.LogInformation("Notificação de cadastro PJ enviada para {Email}", email);
    }

    public async Task SendPartnerRejectionAsync(string email, string reason, CancellationToken ct = default)
    {
        var message = BuildMessage(
            to: email,
            subject: "EcoCell — Cadastro não aprovado",
            body: $"<p>Infelizmente seu cadastro não foi aprovado.</p><p><strong>Motivo:</strong> {reason}</p>");

        await SendAsync(message, ct);
        _logger.LogInformation("E-mail de rejeição enviado para {Email}", email);
    }

    private MimeMessage BuildMessage(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };
        return message;
    }

    private async Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
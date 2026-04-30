using Ecocell.Api.Configurations;
using Ecocell.Api.Enums;
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

    public async Task SendAsync(
        string email,
        EmailType type,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken ct = default)
    {
        var (subject, body) = BuildContent(type, variables);
        var message = BuildMessage(email, subject, body);
        await SendAsync(message, ct);
        _logger.LogInformation("E-mail {Type} enviado para {Email}", type, email);
    }

    private static (string Subject, string Body) BuildContent(EmailType type, IReadOnlyDictionary<string, string>? vars) => type switch
    {
        EmailType.VerificationCode => (
            "EcoCell — Seu código de verificação",
            $"<p>Seu código de verificação é: <strong>{vars?["code"]}</strong></p><p>Válido por 10 minutos.</p>"),

        EmailType.LegalPersonRegistration => (
            "EcoCell — Cadastro recebido",
            "<p>Seu cadastro foi recebido e está em análise. Em breve entraremos em contato.</p>"),

        EmailType.PartnerRejection => (
            "EcoCell — Cadastro não aprovado",
            $"<p>Infelizmente seu cadastro não foi aprovado.</p><p><strong>Motivo:</strong> {vars?["reason"]}</p>"),

        EmailType.PartnerApproval => (
            "EcoCell — Cadastro aprovado",
            "<p>Parabéns! Seu cadastro foi aprovado. Bem-vindo ao EcoCell.</p>"),

        EmailType.PartnerBlock => (
            "EcoCell — Conta suspensa",
            "<p>Sua conta foi suspensa. Entre em contato com o suporte para mais informações.</p>"),

        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

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
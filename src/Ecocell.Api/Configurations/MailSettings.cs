namespace Ecocell.Api.Configurations;

/// <summary>
/// Configurações SMTP para envio de e-mails transacionais via MailKit.
/// Credenciais devem ser definidas em User Secrets (dev) ou variáveis de ambiente (prod).
/// </summary>
public sealed class MailSettings
{
    public const string SectionName = "Mail";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
}

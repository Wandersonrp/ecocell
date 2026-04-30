using Ecocell.Api.Enums;

namespace Ecocell.Api.Services.Email;

/// <summary>
/// Contrato para envio de e-mails transacionais da plataforma.
/// O tipo do e-mail determina o template e as variáveis disponíveis para substituição.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string email,
        EmailType type,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken ct = default);
}
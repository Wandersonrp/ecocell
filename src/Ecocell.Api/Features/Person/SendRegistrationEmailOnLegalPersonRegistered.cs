using Ecocell.Api.Enums;
using Ecocell.Api.Events;
using Ecocell.Api.Services.Email;
using Mediator;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Handler de notificação que reage ao evento <see cref="LegalPersonRegistered"/> enviando
/// e-mails de notificação para a PJ e o Gestor PF. Falhas de infra são logadas e não
/// propagadas — o cadastro já foi efetivado e este handler é fire-and-forget.
/// </summary>
public static class SendRegistrationEmailOnLegalPersonRegistered
{
    /// <summary>
    /// Envia e-mail de "cadastro recebido, aguardando aprovação" para a PJ e o Gestor PF.
    /// </summary>
    public sealed class Handler : INotificationHandler<LegalPersonRegistered>
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<Handler> _logger;

        public Handler(IEmailSender emailSender, ILogger<Handler> logger)
        {
            _emailSender = emailSender;
            _logger = logger;
        }

        /// <summary>
        /// Processa a notificação: envia e-mail para o endereço da PJ e para o Gestor PF.
        /// </summary>
        /// <param name="notification">Dados da PJ recém-cadastrada.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        public async ValueTask Handle(LegalPersonRegistered notification, CancellationToken cancellationToken)
        {
            try
            {
                await _emailSender.SendAsync(notification.LegalPersonEmail, EmailType.LegalPersonRegistration, ct: cancellationToken);
                await _emailSender.SendAsync(notification.ResponsiblePersonEmail, EmailType.LegalPersonRegistration, ct: cancellationToken);

                _logger.LogInformation(
                    "Notificação de cadastro de PJ enviada para {LegalPersonEmail} e {ResponsiblePersonEmail}",
                    notification.LegalPersonEmail,
                    notification.ResponsiblePersonEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falha ao enviar notificação de cadastro de PJ para {LegalPersonEmail} ou {ResponsiblePersonEmail}",
                    notification.LegalPersonEmail,
                    notification.ResponsiblePersonEmail);
            }
        }
    }
}
using Ecocell.Api.Events;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.VerificationCodes;
using Mediator;

namespace Ecocell.Api.Features.Account;

/// <summary>
/// Handler de notificação que reage ao evento <see cref="PersonRegistered"/> gerando e enviando
/// o código OTP de confirmação de e-mail.
/// </summary>
public static class GenerateVerificationCodeOnPersonRegistered
{
    /// <summary>
    /// Gera um código OTP de 6 dígitos, persiste no store com TTL de 10 minutos e dispara o e-mail.
    /// Falhas de infra (store ou e-mail) são logadas mas não propagadas — o cadastro já foi efetivado
    /// e o usuário pode solicitar reenvio via ResendVerificationCode.
    /// </summary>
    public sealed class Handler : INotificationHandler<PersonRegistered>
    {
        private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);

        private readonly IVerificationCodeStore _store;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<Handler> _logger;

        public Handler(IVerificationCodeStore store, IEmailSender emailSender, ILogger<Handler> logger)
        {
            _store = store;
            _emailSender = emailSender;
            _logger = logger;
        }

        /// <summary>
        /// Processa a notificação: gera código OTP, salva no store e envia e-mail de confirmação.
        /// </summary>
        /// <param name="notification">Dados da pessoa recém-cadastrada.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        public async ValueTask Handle(PersonRegistered notification, CancellationToken cancellationToken)
        {
            var code = VerificationCodeGenerator.Generate6Digit();
            var codeHash = VerificationCodeGenerator.Hash(code);
            var key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.EmailConfirmation, notification.Email);

            try
            {
                await _store.SaveAsync(key, codeHash, CodeTtl, cancellationToken);
                await _emailSender.SendVerificationCodeAsync(notification.Email, code, cancellationToken);

                _logger.LogInformation("Código OTP gerado e enviado para {Email}", notification.Email);
            }
            catch (Exception ex)
            {
                // Falha de infra não reverte o cadastro efetivado; usuário pode usar ResendVerificationCode.
                _logger.LogError(ex, "Falha ao gerar/enviar código OTP para {Email}", notification.Email);
            }
        }
    }
}
using Carter;
using Ecocell.Api.Configurations;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static Ecocell.Api.Features.Account.RequestLoginCode;

namespace Ecocell.Api.Features.Account;

/// <summary>
/// Slice responsável por gerar e enviar o código OTP de login ao e-mail informado.
/// </summary>
public static class RequestLoginCode
{
    /// <summary>Comando para solicitação de código OTP de login.</summary>
    public record Command : IRequest<Result>
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>Validador do comando de solicitação de código OTP de login.</summary>
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-mail é obrigatório.")
                .EmailAddress().WithMessage("E-mail inválido.")
                .MaximumLength(255).WithMessage("E-mail deve ter no máximo 255 caracteres.");
        }
    }

    /// <summary>
    /// Handler que gera e envia o código OTP ao e-mail informado.
    /// Para evitar enumeração de e-mails, retorna sucesso (202) mesmo quando a conta não existe
    /// ou não está ativa — nenhum código é gerado nesses casos.
    /// O hash do código usa HMAC-SHA256 com a chave de assinatura JWT para impedir ataques de
    /// dicionário sobre o store caso o Redis seja comprometido.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _dbContext;
        private readonly IVerificationCodeStore _store;
        private readonly IEmailSender _emailSender;
        private readonly IValidator<Command> _validator;
        private readonly ILogger<Handler> _logger;
        private readonly JwtSettings _jwtSettings;

        public Handler(
            AppDbContext dbContext,
            IVerificationCodeStore store,
            IEmailSender emailSender,
            IValidator<Command> validator,
            ILogger<Handler> logger,
            IOptions<JwtSettings> jwtSettings)
        {
            _dbContext = dbContext;
            _store = store;
            _emailSender = emailSender;
            _validator = validator;
            _logger = logger;
            _jwtSettings = jwtSettings.Value;
        }

        /// <summary>
        /// Processa a solicitação de OTP: valida o e-mail, verifica silenciosamente a existência
        /// e o status da conta (anti-enumeração) e, quando ativa, gera e envia o código OTP.
        /// Falhas de infraestrutura são capturadas e logadas sem expor o erro ao cliente.
        /// </summary>
        /// <param name="request">Comando com o e-mail da conta.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="Result"/> indicando sucesso ou erro de validação.</returns>
        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processando solicitação de código OTP de login para {Email}", request.Email);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Erros de validação na solicitação de OTP de login {@Erros}", errors);
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var person = await _dbContext.People
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancellationToken);

            if (person is null || person.PersonStatus != PersonStatus.Active)
            {
                _logger.LogInformation(
                    "Solicitação de OTP de login ignorada para {Email}: conta inexistente ou não ativa (anti-enumeração)",
                    request.Email);
                return Result.Success();
            }

            var code = VerificationCodeGenerator.Generate6Digit();
            var codeHash = VerificationCodeGenerator.HashHmac(code, _jwtSettings.SigningKey);
            var key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.Login, request.Email);

            try
            {
                await _store.SaveAsync(key, codeHash, CodeTtl, cancellationToken);
                await _emailSender.SendVerificationCodeAsync(request.Email, code, cancellationToken);
                _logger.LogInformation("Código OTP de login enviado para {Email}", request.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao salvar ou enviar código OTP de login para {Email}", request.Email);
            }

            return Result.Success();
        }
    }
}

/// <summary>
/// Endpoint para solicitação de código OTP de login.
/// </summary>
public class RequestLoginCodeEndpoint : ICarterModule
{
    /// <summary>Registra a rota POST /api/account/login/request-code.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/account/login/request-code", async ([FromBody] RequestRequestLoginCode request, ISender sender) =>
        {
            var command = new Command { Email = request.Email };
            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status202Accepted);
        })
        .WithTags("Account")
        .WithName("RequestLoginCode")
        .WithSummary("Solicita o envio de um código OTP de login ao e-mail informado.")
        .WithDescription("Gera e envia um código OTP de 6 dígitos. Retorna 202 independentemente de o e-mail existir (anti-enumeração).")
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

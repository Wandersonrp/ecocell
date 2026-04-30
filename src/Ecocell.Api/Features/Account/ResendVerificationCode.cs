using Carter;
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
using static Ecocell.Api.Features.Account.ResendVerificationCode;

namespace Ecocell.Api.Features.Account;

/// <summary>
/// Slice responsável por reenviar o código OTP de confirmação de conta ao e-mail cadastrado.
/// </summary>
public static class ResendVerificationCode
{
    /// <summary>Comando para reenvio do código OTP.</summary>
    public record Command : IRequest<Result>
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>Validador do comando de reenvio de código.</summary>
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-mail é obrigatório.")
                .EmailAddress().WithMessage("E-mail inválido.");
        }
    }

    /// <summary>
    /// Handler que gera um novo código OTP, sobrescreve o anterior no store e dispara novo e-mail.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _dbContext;
        private readonly IVerificationCodeStore _store;
        private readonly IEmailSender _emailSender;
        private readonly IValidator<Command> _validator;
        private readonly ILogger<Handler> _logger;

        public Handler(
            AppDbContext dbContext,
            IVerificationCodeStore store,
            IEmailSender emailSender,
            IValidator<Command> validator,
            ILogger<Handler> logger)
        {
            _dbContext = dbContext;
            _store = store;
            _emailSender = emailSender;
            _validator = validator;
            _logger = logger;
        }

        /// <summary>
        /// Processa o reenvio: valida o e-mail, verifica o status da conta, gera novo código OTP,
        /// persiste no store (sobrescrevendo o anterior) e envia novo e-mail de confirmação.
        /// </summary>
        /// <param name="request">Comando com o e-mail da conta.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="Result"/> indicando sucesso ou o erro encontrado.</returns>
        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processando reenvio de código OTP para {Email}", request.Email);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogError("Erros de validação no reenvio de código {@Erros}", errors);
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var person = await _dbContext.People
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancellationToken);

            if (person is null)
            {
                _logger.LogError("Nenhuma conta encontrada para o e-mail {Email}", request.Email);
                return Result.Failure(Error.NotFound($"Nenhuma conta encontrada para o e-mail {request.Email}."));
            }

            if (person.PersonStatus != PersonStatus.AwaitingConfirmation)
            {
                var message = person.PersonStatus == PersonStatus.Active
                    ? "Conta já confirmada."
                    : $"Não é possível reenviar código para uma conta com status '{person.PersonStatus}'.";
                _logger.LogError("Status inválido para reenvio de código: {Status}", person.PersonStatus);
                return Result.Failure(Error.Conflict(message));
            }

            var code = VerificationCodeGenerator.Generate6Digit();
            var codeHash = VerificationCodeGenerator.Hash(code);
            var key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.EmailConfirmation, request.Email);

            await _store.SaveAsync(key, codeHash, CodeTtl, cancellationToken);
            await _emailSender.SendAsync(
                request.Email,
                EmailType.VerificationCode,
                new Dictionary<string, string> { ["code"] = code },
                cancellationToken);

            _logger.LogInformation("Novo código OTP enviado para {Email}", request.Email);
            return Result.Success();
        }
    }
}

/// <summary>
/// Endpoint para reenvio do código OTP de confirmação.
/// </summary>
public class ResendVerificationCodeEndpoint : ICarterModule
{
    /// <summary>Registra a rota POST /api/account/resend-code.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/account/resend-code", async ([FromBody] RequestResendVerificationCode request, ISender sender) =>
        {
            var command = new Command { Email = request.Email };
            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status202Accepted);
        })
        .WithTags("Account")
        .WithName("ResendVerificationCode")
        .WithSummary("Reenvia o código OTP de confirmação de conta.")
        .WithDescription("Gera novo código OTP, sobrescreve o anterior e envia ao e-mail cadastrado.")
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
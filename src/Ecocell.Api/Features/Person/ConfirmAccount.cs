using System.Security.Cryptography;
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Person.ConfirmAccount;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Slice responsável por confirmar o cadastro de uma pessoa via código OTP enviado ao e-mail.
/// </summary>
public static class ConfirmAccount
{
    /// <summary>Comando para confirmação de conta.</summary>
    public record Command : IRequest<Result>
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>Validador do comando de confirmação de conta.</summary>
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-mail é obrigatório.")
                .EmailAddress().WithMessage("E-mail inválido.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Código é obrigatório.")
                .Matches(@"^\d{6}$").WithMessage("O código deve conter exatamente 6 dígitos numéricos.");
        }
    }

    /// <summary>
    /// Handler que valida o código OTP e ativa a conta da pessoa.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly IVerificationCodeStore _store;
        private readonly IValidator<Command> _validator;
        private readonly ILogger<Handler> _logger;

        public Handler(
            AppDbContext dbContext,
            IVerificationCodeStore store,
            IValidator<Command> validator,
            ILogger<Handler> logger)
        {
            _dbContext = dbContext;
            _store = store;
            _validator = validator;
            _logger = logger;
        }

        /// <summary>
        /// Processa a confirmação de conta: valida o código OTP, transiciona o status para
        /// <see cref="Ecocell.Api.Enums.PersonStatus.Active"/> e remove o código do store.
        /// </summary>
        /// <param name="request">Comando com e-mail e código OTP.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="Result"/> indicando sucesso ou o erro encontrado.</returns>
        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processando confirmação de conta para {Email}", request.Email);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogError("Erros de validação na confirmação de conta {@Erros}", errors);
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
                    : $"Não é possível confirmar uma conta com status '{person.PersonStatus}'.";
                _logger.LogError("Status inválido para confirmação: {Status}", person.PersonStatus);
                return Result.Failure(Error.Conflict(message));
            }

            var key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.EmailConfirmation, request.Email);
            var record = await _store.GetAsync(key, cancellationToken);

            if (record is null)
            {
                _logger.LogError("Código OTP inexistente ou expirado para {Email}", request.Email);
                return Result.Failure(Error.InvalidCredential());
            }

            if (record.Attempts >= 3)
            {
                _logger.LogError("Número máximo de tentativas atingido para {Email}", request.Email);
                return Result.Failure(Error.Forbidden());
            }

            var incomingHash = VerificationCodeGenerator.Hash(request.Code);
            var isValid = CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.CodeHash),
                Convert.FromHexString(incomingHash));

            if (!isValid)
            {
                await _store.IncrementAttemptsAsync(key, cancellationToken);
                _logger.LogError("Código OTP incorreto para {Email}", request.Email);
                return Result.Failure(Error.InvalidCredential());
            }

            person.Confirm();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _store.DeleteAsync(key, cancellationToken);

            _logger.LogInformation("Conta confirmada com sucesso para {Email}", request.Email);
            return Result.Success();
        }
    }
}

/// <summary>
/// Endpoint para confirmação de conta via OTP.
/// </summary>
public class ConfirmAccountEndpoint : ICarterModule
{
    /// <summary>Registra a rota POST /api/account/confirm.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/account/confirm", async ([FromBody] RequestConfirmAccount request, ISender sender) =>
        {
            var command = new Command
            {
                Email = request.Email,
                Code = request.Code
            };

            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("Person")
        .WithName("ConfirmAccount")
        .WithSummary("Confirma o cadastro da pessoa via código OTP.")
        .WithDescription("Valida o código OTP enviado ao e-mail e ativa a conta (AwaitingConfirmation → Active).")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}

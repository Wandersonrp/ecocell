using System.Security.Cryptography;
using Carter;
using Ecocell.Api.Configurations;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.Authentication;
using Ecocell.Api.Services.VerificationCodes;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static Ecocell.Api.Features.Account.VerifyLoginCode;

namespace Ecocell.Api.Features.Account;

/// <summary>
/// Slice responsável por verificar o código OTP de login e emitir o token JWT de acesso.
/// </summary>
public static class VerifyLoginCode
{
    /// <summary>Comando para verificação do código OTP e obtenção do token de acesso.</summary>
    public record Command : IRequest<ResultT<ResponseLogin>>
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>Validador do comando de verificação do código OTP de login.</summary>
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
    /// Handler que valida o código OTP de login e emite um token JWT de acesso para a conta autenticada.
    /// Máximo de 3 tentativas inválidas antes de bloquear; comparação timing-safe via HMAC-SHA256
    /// para evitar timing attacks e ataques de dicionário sobre o store Redis.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, ResultT<ResponseLogin>>
    {
        private readonly AppDbContext _dbContext;
        private readonly IVerificationCodeStore _store;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenStore _refreshTokenStore;
        private readonly IValidator<Command> _validator;
        private readonly ILogger<Handler> _logger;
        private readonly JwtSettings _jwtSettings;

        public Handler(
            AppDbContext dbContext,
            IVerificationCodeStore store,
            IJwtTokenService jwtTokenService,
            IRefreshTokenStore refreshTokenStore,
            IValidator<Command> validator,
            ILogger<Handler> logger,
            IOptions<JwtSettings> jwtSettings)
        {
            _dbContext = dbContext;
            _store = store;
            _jwtTokenService = jwtTokenService;
            _refreshTokenStore = refreshTokenStore;
            _validator = validator;
            _logger = logger;
            _jwtSettings = jwtSettings.Value;
        }

        /// <summary>
        /// Processa a verificação do OTP: valida o comando, verifica existência e status da conta,
        /// valida o código OTP (com limite de 3 tentativas) e emite um token JWT de acesso.
        /// </summary>
        /// <param name="request">Comando com e-mail e código OTP de 6 dígitos.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>
        /// <see cref="ResultT{T}"/> com <see cref="ResponseLogin"/> em caso de sucesso,
        /// ou o erro encontrado (InvalidCredential, Forbidden).
        /// </returns>
        public async ValueTask<ResultT<ResponseLogin>> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processando verificação de código OTP de login para {Email}", request.Email);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Erros de validação na verificação de OTP de login {@Erros}", errors);
                return ResultT<ResponseLogin>.Failure(Error.ErrorOnValidation(errors));
            }

            var person = await _dbContext.People
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancellationToken);

            if (person is null)
            {
                _logger.LogWarning("Tentativa de login para e-mail não cadastrado {Email}", request.Email);
                return ResultT<ResponseLogin>.Failure(Error.InvalidCredential());
            }

            if (person.PersonStatus != PersonStatus.Active)
            {
                _logger.LogWarning(
                    "Tentativa de login para conta com status {Status} para {Email}",
                    person.PersonStatus, request.Email);
                return ResultT<ResponseLogin>.Failure(Error.Forbidden());
            }

            var key = IVerificationCodeStore.BuildKey(VerificationCodePurpose.Login, request.Email);
            var record = await _store.GetAsync(key, cancellationToken);

            if (record is null)
            {
                _logger.LogWarning("Código OTP de login inexistente ou expirado para {Email}", request.Email);
                return ResultT<ResponseLogin>.Failure(Error.InvalidCredential());
            }

            if (record.Attempts >= 3)
            {
                _logger.LogWarning("Número máximo de tentativas de login atingido para {Email}", request.Email);
                return ResultT<ResponseLogin>.Failure(Error.Forbidden());
            }

            var incomingHash = VerificationCodeGenerator.HashHmac(request.Code, _jwtSettings.SigningKey);
            var isValid = CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.CodeHash),
                Convert.FromHexString(incomingHash));

            if (!isValid)
            {
                await _store.IncrementAttemptsAsync(key, cancellationToken);
                _logger.LogWarning("Código OTP de login incorreto para {Email}", request.Email);
                return ResultT<ResponseLogin>.Failure(Error.InvalidCredential());
            }

            await _store.DeleteAsync(key, cancellationToken);

            var refreshTokenResult = _jwtTokenService.GenerateRefreshToken();
            var refreshTtl = TimeSpan.FromDays(_jwtSettings.RefreshTokenLifetimeDays);
            await _refreshTokenStore.SaveAsync(refreshTokenResult.Token, person.Id, refreshTtl, cancellationToken);

            var jwtToken = _jwtTokenService.Generate(person);
            var response = new ResponseLogin
            {
                AccessToken = jwtToken.AccessToken,
                ExpiresAtUtc = jwtToken.ExpiresAtUtc,
                RefreshToken = refreshTokenResult.Token,
                RefreshTokenExpiresAtUtc = refreshTokenResult.ExpiresAtUtc,
            };

            _logger.LogInformation("Login realizado com sucesso para {Email}", request.Email);
            return ResultT<ResponseLogin>.Success(response);
        }
    }
}

/// <summary>
/// Endpoint para verificação do código OTP de login e emissão do token JWT.
/// </summary>
public class VerifyLoginCodeEndpoint : ICarterModule
{
    /// <summary>Registra a rota POST /api/account/login.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/account/login", async ([FromBody] RequestVerifyLoginCode request, ISender sender) =>
        {
            var command = new Command
            {
                Email = request.Email,
                Code = request.Code
            };

            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("Account")
        .WithName("VerifyLoginCode")
        .WithSummary("Verifica o código OTP de login e emite o token JWT de acesso.")
        .WithDescription("Valida o código OTP enviado ao e-mail e retorna um token JWT de acesso em caso de sucesso.")
        .Produces<ResponseLogin>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

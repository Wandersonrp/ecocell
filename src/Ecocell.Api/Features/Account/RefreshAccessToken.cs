using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.Authentication;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Account.RefreshAccessToken;

namespace Ecocell.Api.Features.Account;

/// <summary>
/// Slice responsável por trocar um refresh token válido por um novo par de tokens (one-time-use rotation).
/// </summary>
public static class RefreshAccessToken
{
    /// <summary>Comando para renovação do par de tokens via refresh token.</summary>
    public record Command : IRequest<ResultT<ResponseLogin>>
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>Validador do comando de refresh.</summary>
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh token é obrigatório.");
        }
    }

    /// <summary>
    /// Handler que implementa token rotation: invalida o refresh token atual antes de emitir novo par.
    /// Se a conta estiver inativa, retorna erro mesmo após revogar o token antigo (revoke-always policy).
    /// </summary>
    public sealed class Handler(
        AppDbContext dbContext,
        IRefreshTokenStore refreshTokenStore,
        IJwtTokenService jwtTokenService,
        IValidator<Command> validator,
        ILogger<Handler> logger)
        : IRequestHandler<Command, ResultT<ResponseLogin>>
    {
        public async ValueTask<ResultT<ResponseLogin>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                logger.LogWarning("Erros de validação no refresh de token {@Erros}", errors);
                return ResultT<ResponseLogin>.Failure(Error.ErrorOnValidation(errors));
            }

            var personId = await refreshTokenStore.GetPersonIdAsync(request.RefreshToken, cancellationToken);
            if (personId is null)
            {
                logger.LogWarning("Refresh token inexistente ou expirado.");
                return ResultT<ResponseLogin>.Failure(Error.InvalidCredential());
            }

            await refreshTokenStore.DeleteAsync(request.RefreshToken, cancellationToken);

            var person = await dbContext.People
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == personId.Value, cancellationToken);

            if (person is null)
            {
                logger.LogWarning("Pessoa não encontrada para refresh token. PersonId: {PersonId}", personId);
                return ResultT<ResponseLogin>.Failure(Error.InvalidCredential());
            }

            if (person.PersonStatus != PersonStatus.Active)
            {
                logger.LogWarning(
                    "Refresh token usado para conta com status {Status}. PersonId: {PersonId}",
                    person.PersonStatus, personId);
                return ResultT<ResponseLogin>.Failure(Error.Forbidden());
            }

            var refreshResult = jwtTokenService.GenerateRefreshToken();
            var ttl = refreshResult.ExpiresAtUtc - DateTimeOffset.UtcNow;
            await refreshTokenStore.SaveAsync(refreshResult.Token, personId.Value, ttl, cancellationToken);

            var jwtToken = jwtTokenService.Generate(person);

            logger.LogInformation("Tokens renovados com sucesso. PersonId: {PersonId}", personId);
            return ResultT<ResponseLogin>.Success(new ResponseLogin
            {
                AccessToken = jwtToken.AccessToken,
                ExpiresAtUtc = jwtToken.ExpiresAtUtc,
                RefreshToken = refreshResult.Token,
                RefreshTokenExpiresAtUtc = refreshResult.ExpiresAtUtc,
            });
        }
    }
}

/// <summary>
/// Endpoint para renovação do par de tokens via refresh token.
/// </summary>
public class RefreshAccessTokenEndpoint : ICarterModule
{
    /// <summary>Registra a rota POST api/account/refresh-token.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/account/refresh-token", async ([FromBody] RequestRefreshAccessToken request, ISender sender) =>
        {
            var command = new Command
            {
                RefreshToken = request.RefreshToken
            };

            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("Account")
        .WithName("RefreshAccessToken")
        .WithSummary("Renova o par de tokens usando o refresh token.")
        .WithDescription("Invalida o refresh token atual e emite novo par (access token + refresh token). Token de uso único — não reutilizar após chamada.")
        .Produces<ResponseLogin>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireRateLimiting("public-ip");
    }
}

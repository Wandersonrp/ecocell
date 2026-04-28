using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Extensions;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Slice para consulta do perfil da pessoa física autenticada via endpoint GET /api/natural-person/me.
/// </summary>
public static class GetNaturalPersonProfile
{
    /// <summary>
    /// Consulta que encapsula o identificador da pessoa física cujo perfil será retornado.
    /// </summary>
    /// <param name="PersonId">Identificador único da pessoa física.</param>
    public record Query(Guid PersonId) : IRequest<ResultT<ResponseNaturalPersonProfile>>;

    /// <summary>
    /// Validador da query, garantindo que o identificador da pessoa não seja vazio.
    /// </summary>
    public class Validator : AbstractValidator<Query>
    {
        /// <summary>Inicializa as regras de validação da query.</summary>
        public Validator()
        {
            RuleFor(x => x.PersonId)
                .NotEmpty().WithMessage("O identificador da pessoa física é obrigatório.");
        }
    }

    /// <summary>
    /// Handler responsável por carregar o perfil da pessoa física via projection EF Core.
    /// </summary>
    public sealed class Handler : IRequestHandler<Query, ResultT<ResponseNaturalPersonProfile>>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Query> _validator;

        /// <summary>
        /// Inicializa o handler com as dependências necessárias.
        /// </summary>
        /// <param name="dbContext">Contexto de banco de dados.</param>
        /// <param name="logger">Logger estruturado.</param>
        /// <param name="validator">Validador da query.</param>
        public Handler(AppDbContext dbContext, ILogger<Handler> logger, IValidator<Query> validator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
        }

        /// <summary>
        /// Executa a busca do perfil da pessoa física, validando o identificador antes de consultar o banco.
        /// </summary>
        /// <param name="request">Query com o identificador da pessoa física.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="ResultT{T}"/> com o perfil ou o erro encontrado.</returns>
        public async ValueTask<ResultT<ResponseNaturalPersonProfile>> Handle(Query request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Buscando perfil da pessoa física {PersonId}", request.PersonId);

            var validation = _validator.Validate(request);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return ResultT<ResponseNaturalPersonProfile>.Failure(Error.ErrorOnValidation(errors));
            }

            var profile = await _dbContext.NaturalPeople
                .AsNoTracking()
                .Where(np => np.Id == request.PersonId)
                .Select(np => new ResponseNaturalPersonProfile
                {
                    Id           = np.Id,
                    FullName     = np.FullName,
                    Email        = np.Email,
                    Cpf          = np.Cpf,
                    BirthDate    = np.BirthDate,
                    Role         = (Ecocell.Shared.Enums.Role)(int)np.Role,
                    Journey      = (Ecocell.Shared.Enums.Journey)(int)np.Journey,
                    PersonType   = (Ecocell.Shared.Enums.PersonType)(int)np.PersonType,
                    PersonStatus = (Ecocell.Shared.Enums.PersonStatus)(int)np.PersonStatus,
                    CreatedAt    = np.CreatedAt,
                    UpdatedAt    = np.UpdatedAt,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (profile is null)
            {
                _logger.LogWarning("Pessoa física {PersonId} não encontrada.", request.PersonId);
                return ResultT<ResponseNaturalPersonProfile>.Failure(
                    Error.NotFound($"Pessoa física {request.PersonId} não encontrada."));
            }

            _logger.LogInformation("Perfil da pessoa física {PersonId} carregado com sucesso.", request.PersonId);
            return ResultT<ResponseNaturalPersonProfile>.Success(profile);
        }
    }
}

/// <summary>
/// Endpoint Carter para GET /api/natural-person/me — retorna o perfil da pessoa física autenticada.
/// </summary>
public class GetNaturalPersonProfileEndpoint : ICarterModule
{
    /// <summary>Registra a rota do endpoint no pipeline da aplicação.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("api/natural-person/me", async (ClaimsPrincipal user, ISender sender) =>
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sid);
            if (!Guid.TryParse(sub, out var personId))
                return ResultT<ResponseNaturalPersonProfile>.Failure(Error.Unauthorized())
                    .ToProcessResult(StatusCodes.Status200OK);

            var result = await sender.Send(new GetNaturalPersonProfile.Query(personId));
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("Person")
        .WithName("GetNaturalPersonProfile")
        .WithSummary("Retorna o perfil da pessoa física autenticada.")
        .WithDescription("Carrega os dados de perfil da pessoa física identificada pela claim 'sub' do JWT.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces<ResponseNaturalPersonProfile>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

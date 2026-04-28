using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Extensions;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Slice para atualização do perfil da pessoa física autenticada via endpoint PUT /api/natural-person/me.
/// </summary>
public static class UpdateNaturalPerson
{
    /// <summary>
    /// Comando que encapsula os dados de atualização do perfil da pessoa física.
    /// </summary>
    public record Command : IRequest<ResultT<ResponseNaturalPersonProfile>>
    {
        /// <summary>Identificador da pessoa física (extraído do JWT).</summary>
        public Guid PersonId { get; set; }

        /// <summary>Novo nome completo.</summary>
        public string FullName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validador do comando de atualização do perfil.
    /// </summary>
    public class Validator : AbstractValidator<Command>
    {
        /// <summary>Inicializa as regras de validação do comando.</summary>
        public Validator()
        {
            RuleFor(x => x.PersonId)
                .NotEmpty().WithMessage("O identificador da pessoa física é obrigatório.");

            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Nome completo é obrigatório.")
                .MinimumLength(3).WithMessage("Nome completo deve conter no mínimo 3 caracteres.")
                .MaximumLength(100).WithMessage("Nome completo deve conter no máximo 100 caracteres.");
        }
    }

    /// <summary>
    /// Handler responsável por atualizar o nome completo da pessoa física autenticada.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, ResultT<ResponseNaturalPersonProfile>>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;

        /// <summary>
        /// Inicializa o handler com as dependências necessárias.
        /// </summary>
        /// <param name="dbContext">Contexto de banco de dados.</param>
        /// <param name="logger">Logger estruturado.</param>
        /// <param name="validator">Validador do comando.</param>
        public Handler(AppDbContext dbContext, ILogger<Handler> logger, IValidator<Command> validator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
        }

        /// <summary>
        /// Executa a atualização do perfil da pessoa física, validando os dados antes de persistir.
        /// </summary>
        /// <param name="request">Comando com os dados de atualização.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="ResultT{T}"/> com o perfil atualizado ou o erro encontrado.</returns>
        public async ValueTask<ResultT<ResponseNaturalPersonProfile>> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Atualizando perfil da pessoa física {PersonId}", request.PersonId);

            var validation = _validator.Validate(request);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Dados inválidos para atualização do perfil {PersonId}: {@Erros}", request.PersonId, errors);
                return ResultT<ResponseNaturalPersonProfile>.Failure(Error.ErrorOnValidation(errors));
            }

            var person = await _dbContext.NaturalPeople
                .FirstOrDefaultAsync(np => np.Id == request.PersonId, cancellationToken);

            if (person is null)
            {
                _logger.LogWarning("Pessoa física {PersonId} não encontrada para atualização.", request.PersonId);
                return ResultT<ResponseNaturalPersonProfile>.Failure(
                    Error.NotFound($"Pessoa física {request.PersonId} não encontrada."));
            }

            person.Update(request.FullName);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Perfil da pessoa física {PersonId} atualizado com sucesso.", request.PersonId);

            var profile = new ResponseNaturalPersonProfile
            {
                Id           = person.Id,
                FullName     = person.FullName,
                Email        = person.Email,
                Cpf          = person.Cpf,
                BirthDate    = person.BirthDate,
                Role         = (Ecocell.Shared.Enums.Role)(int)person.Role,
                Journey      = (Ecocell.Shared.Enums.Journey)(int)person.Journey,
                PersonType   = (Ecocell.Shared.Enums.PersonType)(int)person.PersonType,
                PersonStatus = (Ecocell.Shared.Enums.PersonStatus)(int)person.PersonStatus,
                CreatedAt    = person.CreatedAt,
                UpdatedAt    = person.UpdatedAt,
            };

            return ResultT<ResponseNaturalPersonProfile>.Success(profile);
        }
    }
}

/// <summary>
/// Endpoint Carter para PUT /api/natural-person/me — atualiza o perfil da pessoa física autenticada.
/// </summary>
public class UpdateNaturalPersonEndpoint : ICarterModule
{
    /// <summary>Registra a rota do endpoint no pipeline da aplicação.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("api/natural-person/me", async (
            [FromBody] RequestUpdateNaturalPerson req,
            ClaimsPrincipal user,
            ISender sender) =>
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sid);
            if (!Guid.TryParse(sub, out var personId))
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");

            var result = await sender.Send(new UpdateNaturalPerson.Command
            {
                PersonId = personId,
                FullName = req.FullName
            });

            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("Person")
        .WithName("UpdateNaturalPersonProfile")
        .WithSummary("Atualiza o perfil da pessoa física autenticada.")
        .WithDescription("Atualiza o nome completo da pessoa física identificada pela claim 'sub' do JWT.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces<ResponseNaturalPersonProfile>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
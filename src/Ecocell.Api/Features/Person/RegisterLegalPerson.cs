using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Events;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.External;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Person.RegisterLegalPerson;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Slice responsável pelo cadastro de uma nova Pessoa Jurídica (Ponto de Coleta ou Coletor)
/// por um Gestor PF autenticado. Status inicial: <see cref="PersonStatus.PendingApproval"/> (RN007).
/// </summary>
public static class RegisterLegalPerson
{
    /// <summary>
    /// Dados de endereço internos ao slice — não expostos na borda HTTP.
    /// </summary>
    public record AddressCommand
    {
        public string Street { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? Complement { get; set; }
        public string Neighborhood { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comando interno para cadastro de PJ. Inclui <see cref="ResponsiblePersonId"/> extraído
    /// do JWT pelo endpoint — nunca exposto no DTO público.
    /// </summary>
    public record Command : IRequest<Result>
    {
        public string Cnpj { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string TradeName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Journey Journey { get; set; }
        public Guid ResponsiblePersonId { get; set; }
        public AddressCommand Address { get; set; } = new();
    }

    /// <summary>
    /// Valida o comando de cadastro de PJ garantindo CNPJ válido, e-mail válido, campos
    /// obrigatórios de endereço e que a <see cref="Journey"/> seja <see cref="Journey.CollectPoint"/>
    /// ou <see cref="Journey.Collector"/> (RN003).
    /// </summary>
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Cnpj).IsValidCnpj();

            RuleFor(x => x.LegalName)
                .NotEmpty().WithMessage("Razão social é obrigatória.")
                .MaximumLength(255).WithMessage("Razão social deve conter no máximo 255 caracteres.");

            RuleFor(x => x.TradeName)
                .NotEmpty().WithMessage("Nome fantasia é obrigatório.")
                .MaximumLength(255).WithMessage("Nome fantasia deve conter no máximo 255 caracteres.");

            RuleFor(x => x.Email).IsValidEmail();

            RuleFor(x => x.Journey)
                .Must(j => j == Journey.CollectPoint || j == Journey.Collector)
                .WithMessage("Papel da pessoa jurídica deve ser Ponto de Coleta ou Coletor. (RN003)");

            RuleFor(x => x.Address.Street)
                .NotEmpty().WithMessage("Logradouro é obrigatório.")
                .MaximumLength(255).WithMessage("Logradouro deve conter no máximo 255 caracteres.");

            RuleFor(x => x.Address.Number)
                .NotEmpty().WithMessage("Número é obrigatório.")
                .MaximumLength(20).WithMessage("Número deve conter no máximo 20 caracteres.");

            RuleFor(x => x.Address.Complement)
                .MaximumLength(100).WithMessage("Complemento deve conter no máximo 100 caracteres.")
                .When(x => x.Address.Complement is not null);

            RuleFor(x => x.Address.Neighborhood)
                .NotEmpty().WithMessage("Bairro é obrigatório.")
                .MaximumLength(100).WithMessage("Bairro deve conter no máximo 100 caracteres.");

            RuleFor(x => x.Address.City)
                .NotEmpty().WithMessage("Cidade é obrigatória.")
                .MaximumLength(100).WithMessage("Cidade deve conter no máximo 100 caracteres.");

            RuleFor(x => x.Address.State)
                .Length(2).WithMessage("Estado deve conter exatamente 2 caracteres (sigla UF).");

            RuleFor(x => x.Address.ZipCode)
                .Length(8).WithMessage("CEP deve conter exatamente 8 dígitos (sem máscara).");
        }
    }

    /// <summary>
    /// Processa o cadastro da PJ: valida, verifica unicidade de CNPJ e e-mail, confirma
    /// que o Gestor PF existe e está ativo, geocodifica o endereço (RN009), persiste
    /// PJ + Address e publica <see cref="LegalPersonRegistered"/>.
    /// Geocoding indisponível (GEOCODING_UNAVAILABLE) não bloqueia o cadastro — coordenadas
    /// ficam nulas. Endereço não encontrado (GEOCODING_NOT_FOUND) bloqueia o cadastro.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly IPublisher _publisher;
        private readonly IGeocodingService _geocodingService;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Command> validator,
            IPublisher publisher,
            IGeocodingService geocodingService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _publisher = publisher;
            _geocodingService = geocodingService;
        }

        /// <summary>
        /// Executa o cadastro da Pessoa Jurídica seguindo a sequência: validação de input,
        /// unicidade de CNPJ/e-mail, verificação do Gestor PF, geocodificação do endereço e persistência.
        /// </summary>
        /// <param name="request">Comando com os dados da PJ e do endereço.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="Result"/> indicando sucesso ou o erro encontrado.</returns>
        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processando cadastro da pessoa jurídica {@LegalPerson}", request);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogError("Erros de validação ao cadastrar pessoa jurídica {@Erros}", errors);
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var cnpjExists = await _dbContext.LegalPeople
                .AnyAsync(lp => lp.Cnpj == request.Cnpj, cancellationToken);
            if (cnpjExists)
            {
                _logger.LogError("CNPJ {Cnpj} já cadastrado", request.Cnpj);
                return Result.Failure(Error.Conflict($"Já existe uma pessoa jurídica cadastrada com o CNPJ {request.Cnpj}."));
            }

            var emailExists = await _dbContext.People
                .AnyAsync(p => p.Email == request.Email, cancellationToken);
            if (emailExists)
            {
                _logger.LogError("E-mail {Email} já cadastrado", request.Email);
                return Result.Failure(Error.Conflict($"Já existe uma pessoa cadastrada com o e-mail {request.Email}."));
            }

            var responsiblePerson = await _dbContext.NaturalPeople
                .AsNoTracking()
                .FirstOrDefaultAsync(np => np.Id == request.ResponsiblePersonId, cancellationToken);
            if (responsiblePerson is null)
            {
                _logger.LogError("Gestor {ResponsiblePersonId} não encontrado", request.ResponsiblePersonId);
                return Result.Failure(Error.NotFound($"Pessoa física {request.ResponsiblePersonId} não encontrada."));
            }

            if (responsiblePerson.PersonStatus != PersonStatus.Active)
            {
                _logger.LogError("Gestor {ResponsiblePersonId} não está ativo", request.ResponsiblePersonId);
                return Result.Failure(Error.Forbidden());
            }

            decimal? lat = null, lng = null;
            var geoRequest = new GeocodingRequest(
                request.Address.Street,
                request.Address.Number,
                request.Address.City,
                request.Address.State,
                request.Address.ZipCode);

            var geoResult = await _geocodingService.GeocodeAsync(geoRequest, cancellationToken);

            if (geoResult.IsFailure)
            {
                if (geoResult.Error.Code == ErrorCodes.GeocodingNotFound)
                    return Result.Failure(geoResult.Error);

                _logger.LogWarning("Geocoding indisponível para endereço {@Address}. Motivo: {Motivo}", request.Address, geoResult.Error.Message);
            }
            else
            {
                lat = geoResult.Value.Latitude;
                lng = geoResult.Value.Longitude;
            }

            var address = new Address(
                request.Address.Street,
                request.Address.Number,
                request.Address.Neighborhood,
                request.Address.City,
                request.Address.State,
                request.Address.ZipCode,
                request.Address.Complement,
                lat,
                lng);

            var legalPerson = new LegalPerson(
                request.LegalName,
                request.TradeName,
                request.Cnpj,
                request.Email,
                request.Journey,
                address.Id,
                request.ResponsiblePersonId);

            _dbContext.Addresses.Add(address);
            _dbContext.LegalPeople.Add(legalPerson);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(
                new LegalPersonRegistered(legalPerson.Id, legalPerson.Email, responsiblePerson.Email),
                cancellationToken);

            _logger.LogInformation("Pessoa jurídica cadastrada com sucesso {@LegalPerson}", legalPerson);
            return Result.Success();
        }
    }
}

/// <summary>
/// Endpoint Carter para POST /api/legal-person — cadastra nova Pessoa Jurídica.
/// Requer autenticação JWT; extrai <c>ResponsiblePersonId</c> da claim <c>sid</c>.
/// </summary>
public class RegisterLegalPersonEndpoint : ICarterModule
{
    /// <summary>Registra a rota do endpoint no pipeline da aplicação.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/legal-person", async (
            [FromBody] RequestRegisterLegalPerson request,
            ClaimsPrincipal user,
            ISender sender) =>
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sid);
            if (!Guid.TryParse(sub, out var responsiblePersonId))
                return Result.Failure(Error.Unauthorized()).ToProcessResult(StatusCodes.Status401Unauthorized);

            var command = new Command
            {
                Cnpj = request.Cnpj,
                LegalName = request.LegalName,
                TradeName = request.TradeName,
                Email = request.Email,
                Journey = (Journey)(int)request.Journey,
                ResponsiblePersonId = responsiblePersonId,
                Address = new AddressCommand
                {
                    Street = request.Address.Street,
                    Number = request.Address.Number,
                    Complement = request.Address.Complement,
                    Neighborhood = request.Address.Neighborhood,
                    City = request.Address.City,
                    State = request.Address.State,
                    ZipCode = request.Address.ZipCode
                }
            };

            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status201Created);
        })
        .WithTags("Person")
        .WithName("RegisterLegalPerson")
        .WithSummary("Registra uma nova pessoa jurídica vinculada ao gestor autenticado.")
        .WithDescription("Cria registro de LegalPerson com endereço. Status inicial: PendingApproval (RN007). Requer JWT do Gestor PF.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }
}
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Events;
using Ecocell.Api.Extensions;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Person.RegisterNaturalPerson;

namespace Ecocell.Api.Features.Person;

public static class RegisterNaturalPerson
{    
    public record Command : IRequest<Result>
    {
        public string Email { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public Journey Journey { get; set; }
        public DateOnly BirthDate { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email).IsValidEmail();

            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Nome completo é obrigatório.")
                .MinimumLength(3).WithMessage("Nome completo deve conter no mínimo 3 caracteres;")
                .MaximumLength(100).WithMessage("Nome completo deve conter no máximo 100 caracteres.");

            RuleFor(x => x.Journey).IsInEnum().WithMessage("Valor inválido para Journey.");            
            RuleFor(x => x.Cpf).IsValidCpf();

            RuleFor(x => x.BirthDate)
                .NotNull().WithMessage("Data de Nascimento é obrigatória.")
                .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16))).WithMessage("Idade mínima para cadastro na plataforma deve ser de 16 anos.");                
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly IPublisher _publisher;

        public Handler(AppDbContext dbContext, ILogger<Handler> logger, IValidator<Command> validator, IPublisher publisher)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _publisher = publisher;
        }

        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {            
            _logger.LogInformation("Processando o cadastro da pessoa física {@NaturalPerson}", request);

            var validationResult = _validator.Validate(request);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogError("Ocorreram um ou mais erros ao validar os dados da requisição {@Erros}", errors);
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var cpfExists = await _dbContext.NaturalPeople                
                .AnyAsync(np => np.Cpf == request.Cpf, cancellationToken);            

            if (cpfExists)
            {
                _logger.LogError("Já existe uma pessoa física cadastrada com o CPF {CPF}", request.Cpf);
                return Result.Failure(Error.Conflict($"Já existe uma pessoa física cadastrada com o CPF {request.Cpf}"));
            }

            var emailExists = await _dbContext.People
                .AnyAsync(p => p.Email == request.Email, cancellationToken);

            if (emailExists)
            {
                _logger.LogError("Já existe uma pessoa cadastrada com o email {Email}", request.Email);
                return Result.Failure(Error.Conflict($"Já existe uma pessoa cadastrada com o Email {request.Email}"));
            }

            var person = new NaturalPerson(
                request.FullName, 
                request.Cpf, 
                request.BirthDate, 
                Role.User, 
                request.Email, 
                request.Journey);

            _dbContext.People.Add(person);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(new PersonRegistered(person.Id, person.Email), cancellationToken);

            _logger.LogInformation("Pessoa física cadastrada com sucesso {@NaturalPerson}", person);
            return Result.Success();
        }
    }
}

public class RegisterNaturalPersonEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/natural-person", async ([FromBody] RequestRegisterNaturalPerson request, ISender sender) =>
        {
            var command = new Command
            {
                Email = request.Email,
                FullName = request.FullName,
                Cpf = request.Cpf,
                Journey = (Journey)request.Journey,
                BirthDate = request.BirthDate
            };
            
            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status201Created);
        })
        .WithTags("Person")
        .WithName("RegisterNaturalPerson")
        .WithSummary("Registra uma nova pessoa física no sistema.")
        .WithDescription("Cria um registro de NaturalPerson vinculado à jornada de descarte selecionada.")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

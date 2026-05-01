using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Extensions;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Admin.UpdateLegalPersonCoordinates;

namespace Ecocell.Api.Features.Admin;

public static class UpdateLegalPersonCoordinates
{
    public record Command : IRequest<Result>
    {
        public Guid LegalPersonId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LegalPersonId).NotEmpty().WithMessage("LegalPersonId é obrigatório.");
            RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).WithMessage("Latitude deve estar entre -90 e 90.");
            RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).WithMessage("Longitude deve estar entre -180 e 180.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;

        public Handler(AppDbContext dbContext, ILogger<Handler> logger, IValidator<Command> validator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
        }

        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Atualizando coordenadas da pessoa jurídica {LegalPersonId}", request.LegalPersonId);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var legalPerson = await _dbContext.LegalPeople
                .Include(lp => lp.Address)
                .FirstOrDefaultAsync(lp => lp.Id == request.LegalPersonId, cancellationToken);

            if (legalPerson is null)
            {
                _logger.LogError("Pessoa jurídica {LegalPersonId} não encontrada", request.LegalPersonId);
                return Result.Failure(Error.NotFound($"Pessoa jurídica {request.LegalPersonId} não encontrada."));
            }

            if (legalPerson.Address is null)
            {
                _logger.LogError("Endereço da pessoa jurídica {LegalPersonId} não encontrado", request.LegalPersonId);
                return Result.Failure(Error.NotFound($"Endereço da pessoa jurídica {request.LegalPersonId} não encontrado."));
            }

            legalPerson.Address.UpdateCoordinates(request.Latitude, request.Longitude);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Coordenadas da pessoa jurídica {LegalPersonId} atualizadas com sucesso", request.LegalPersonId);
            return Result.Success();
        }
    }
}

public class UpdateLegalPersonCoordinatesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapMethods("api/legal-person/{legalPersonId}/address/coordinates",
            [HttpMethods.Patch],
            async (
                Guid legalPersonId,
                [FromBody] RequestUpdateLegalPersonCoordinates request,
                ISender sender) =>
            {
                var command = new Command
                {
                    LegalPersonId = legalPersonId,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                };

                var result = await sender.Send(command);
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("Admin")
            .WithName("UpdateLegalPersonCoordinates")
            .WithSummary("Atualiza as coordenadas geográficas do endereço de uma pessoa jurídica.")
            .WithDescription("Permite ao administrador definir manualmente latitude e longitude quando o geocoding automático falhar.")
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);
    }
}
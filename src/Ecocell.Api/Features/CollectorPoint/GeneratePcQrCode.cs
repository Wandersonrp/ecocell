using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Features.CollectorPoint;

public static class GeneratePcQrCode
{
    public record Query(Guid CollectorPointId) : IRequest<ResultT<ResponseCollectorPointQrCode>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.CollectorPointId)
                .NotEmpty()
                .WithMessage("O identificador do ponto de coleta é obrigatório.");
        }
    }

    public sealed class Handler : IRequestHandler<Query, ResultT<ResponseCollectorPointQrCode>>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Query> _validator;
        private readonly ICurrentUserService _currentUserService;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Query> validator,
            ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _currentUserService = currentUserService;
        }

        public async ValueTask<ResultT<ResponseCollectorPointQrCode>> Handle(Query request, CancellationToken ct)
        {
            var validation = await _validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                var messages = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return ResultT<ResponseCollectorPointQrCode>.Failure(Error.ErrorOnValidation(messages));
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync(ct);
            if (currentUser is null
                || currentUser.PersonType != PersonType.NaturalPerson
                || currentUser.PersonStatus != PersonStatus.Active
                || currentUser.Role != Role.User)
            {
                _logger.LogWarning("Chamador inelegível ao gerar QR de ponto de coleta.");
                return ResultT<ResponseCollectorPointQrCode>.Failure(Error.Forbidden());
            }

            var collectPoint = await _dbContext.LegalPeople
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    lp => lp.Id == request.CollectorPointId && lp.Journey == Journey.CollectPoint,
                    ct);

            if (collectPoint is null || collectPoint.ResponsiblePersonId != currentUser.Id)
                return ResultT<ResponseCollectorPointQrCode>.Failure(
                    Error.NotFound("Ponto de coleta não encontrado."));

            if (collectPoint.PersonStatus != PersonStatus.Active)
                return ResultT<ResponseCollectorPointQrCode>.Failure(
                    Error.Conflict("Ponto de coleta não está disponível para emitir QR Code."));

            return ResultT<ResponseCollectorPointQrCode>.Success(
                new ResponseCollectorPointQrCode { Qr = $"ecocell://pc/{collectPoint.Id}" });
        }
    }
}

public class GeneratePcQrCodeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("api/collector-points/{id:guid}/qr", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GeneratePcQrCode.Query(id));
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("CollectorPoint")
        .WithName("GeneratePcQrCode")
        .WithSummary("Gera a URI de QR Code do ponto de coleta (dono autenticado, ponto ativo).")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces<ResponseCollectorPointQrCode>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}

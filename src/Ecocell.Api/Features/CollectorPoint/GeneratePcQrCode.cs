using Carter;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;

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
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Query> _validator;
        private readonly ICollectorPointAccessGuard _accessGuard;

        public Handler(
            ILogger<Handler> logger,
            IValidator<Query> validator,
            ICollectorPointAccessGuard accessGuard)
        {
            _logger = logger;
            _validator = validator;
            _accessGuard = accessGuard;
        }

        public async ValueTask<ResultT<ResponseCollectorPointQrCode>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                var messages = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return ResultT<ResponseCollectorPointQrCode>.Failure(Error.ErrorOnValidation(messages));
            }

            var access = await _accessGuard.EnsureResponsibleActiveAsync(
                request.CollectorPointId,
                cancellationToken);
            if (access.IsFailure)
            {
                _logger.LogWarning(
                    "Acesso negado ao gerar QR do Ponto de Coleta {CollectorPointId}. Código: {Code}.",
                    request.CollectorPointId,
                    access.Error.Code);
                return ResultT<ResponseCollectorPointQrCode>.Failure(access.Error);
            }

            return ResultT<ResponseCollectorPointQrCode>.Success(
                new ResponseCollectorPointQrCode
                {
                    Qr = $"ecocell://pc/{request.CollectorPointId}",
                });
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

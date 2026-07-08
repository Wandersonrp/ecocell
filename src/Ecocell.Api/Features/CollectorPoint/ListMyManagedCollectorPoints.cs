using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Features.CollectorPoint;

public static class ListMyManagedCollectorPoints
{
    public record Query : IRequest<ResultT<ResponseManagedCollectorPointList>>;

    public sealed class Handler : IRequestHandler<Query, ResultT<ResponseManagedCollectorPointList>>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly ICurrentUserService _currentUserService;

        public Handler(AppDbContext dbContext, ILogger<Handler> logger, ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async ValueTask<ResultT<ResponseManagedCollectorPointList>> Handle(Query request, CancellationToken ct)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync(ct);

            if (currentUser is null
                || currentUser.PersonStatus != PersonStatus.Active
                || currentUser.PersonType != PersonType.NaturalPerson)
            {
                _logger.LogWarning("Chamador não é pessoa física ativa ao listar pontos de coleta geridos.");
                return ResultT<ResponseManagedCollectorPointList>.Failure(Error.Forbidden());
            }

            var items = await _dbContext.LegalPeople
                .AsNoTracking()
                .Where(lp => lp.ResponsiblePersonId == currentUser.Id
                             && lp.Journey == Journey.CollectPoint)
                .OrderBy(lp => lp.TradeName)
                .Select(lp => new ResponseManagedCollectorPoint
                {
                    Id = lp.Id,
                    TradeName = lp.TradeName,
                    LegalName = lp.LegalName,
                    Cnpj = lp.Cnpj,
                    Status = (Ecocell.Shared.Enums.PersonStatus)(int)lp.PersonStatus,
                })
                .ToListAsync(ct);

            _logger.LogInformation(
                "Responsável {ResponsibleId} tem {Count} pontos de coleta.",
                currentUser.Id, items.Count);

            return ResultT<ResponseManagedCollectorPointList>.Success(
                new ResponseManagedCollectorPointList { Items = items });
        }
    }
}

public class ListMyManagedCollectorPointsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("api/collector-points/me", async (ISender sender) =>
        {
            var result = await sender.Send(new ListMyManagedCollectorPoints.Query());
            return result.ToProcessResult(StatusCodes.Status200OK);
        })
        .WithTags("CollectorPoint")
        .WithName("ListMyManagedCollectorPoints")
        .WithSummary("Lista os pontos de coleta que a pessoa física autenticada gerencia.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces<ResponseManagedCollectorPointList>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

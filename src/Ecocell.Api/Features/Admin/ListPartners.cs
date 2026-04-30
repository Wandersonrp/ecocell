using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests.Admin;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Admin.ListPartners;

namespace Ecocell.Api.Features.Admin;

public static class ListPartners
{
    public record Command : IRequest<ResultT<ResponsePartnerList>>
    {
        public PersonStatus? Status { get; set; }
        public Journey? Journey { get; set; }
        public Guid? Cursor { get; set; }
        public int PageSize { get; set; } = 20;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("PageSize deve ser entre 1 e 100.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, ResultT<ResponsePartnerList>>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly ICurrentUserService _currentUserService;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Command> validator,
            ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _currentUserService = currentUserService;
        }

        public async ValueTask<ResultT<ResponsePartnerList>> Handle(Command request, CancellationToken ct)
        {
            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ResultT<ResponsePartnerList>.Failure(Error.ErrorOnValidation(errors));
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync(ct);

            if (currentUser is null || currentUser.PersonStatus != PersonStatus.Active)
            {
                _logger.LogError("Usuário não autenticado ou inativo tentou listar parceiros.");
                return ResultT<ResponsePartnerList>.Failure(Error.Forbidden());
            }

            if (currentUser.Role != Role.Admin)
            {
                _logger.LogError("Usuário {UserId} sem permissão de admin tentou listar parceiros.", currentUser.Id);
                return ResultT<ResponsePartnerList>.Failure(Error.Forbidden());
            }

            var query = _dbContext.LegalPeople
                .Where(lp => !request.Status.HasValue || lp.PersonStatus == request.Status.Value)
                .Where(lp => !request.Journey.HasValue || lp.Journey == request.Journey.Value)
                .Where(lp => request.Cursor == null || lp.Id.CompareTo(request.Cursor.Value) > 0)
                .OrderBy(lp => lp.Id);

            var items = await query
                .Take(request.PageSize + 1)
                .Select(lp => new ResponsePartnerItem
                {
                    ExternalId = lp.Id,
                    LegalName = lp.LegalName,
                    TradeName = lp.TradeName,
                    Cnpj = lp.Cnpj,
                    Email = lp.Email,
                    Status = (Ecocell.Shared.Enums.PersonStatus)lp.PersonStatus,
                    Journey = (Ecocell.Shared.Enums.Journey)lp.Journey,
                    CreatedAt = lp.CreatedAt
                })
                .ToListAsync(ct);

            var hasMore = items.Count > request.PageSize;
            if (hasMore) items.RemoveAt(items.Count - 1);
            var nextCursor = hasMore ? items.Last().ExternalId : (Guid?)null;

            _logger.LogInformation("Listagem de parceiros retornou {Count} itens.", items.Count);

            return ResultT<ResponsePartnerList>.Success(new ResponsePartnerList
            {
                Items = items,
                NextCursor = nextCursor,
                HasMore = hasMore
            });
        }
    }
}

public class ListPartnersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("api/v1/admin/partners",
            async ([AsParameters] RequestListPartners req, ISender sender) =>
            {
                var command = new ListPartners.Command
                {
                    Status = req.Status.HasValue ? (PersonStatus?)req.Status.Value : null,
                    Journey = req.Journey.HasValue ? (Journey?)req.Journey.Value : null,
                    Cursor = req.Cursor,
                    PageSize = req.PageSize ?? 20
                };

                var result = await sender.Send(command);
                return result.ToProcessResult(StatusCodes.Status200OK);
            })
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .WithTags("Admin")
            .WithName("ListPartners")
            .WithSummary("Lista parceiros PJ com filtros e paginação por cursor.")
            .Produces(StatusCodes.Status200OK, typeof(ResponsePartnerList))
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Admin.ApprovePartner;

namespace Ecocell.Api.Features.Admin;

public static class ApprovePartner
{
    public record Command : IRequest<Result>
    {
        public Guid PartnerId { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PartnerId).NotEmpty().WithMessage("PartnerId é obrigatório.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailSender _emailSender;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Command> validator,
            ICurrentUserService currentUserService,
            IEmailSender emailSender)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _currentUserService = currentUserService;
            _emailSender = emailSender;
        }

        public async ValueTask<Result> Handle(Command request, CancellationToken ct)
        {
            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync(ct);

            if (currentUser is null || currentUser.PersonStatus != PersonStatus.Active)
            {
                _logger.LogError("Usuário não autenticado ou inativo tentou aprovar parceiro.");
                return Result.Failure(Error.Forbidden());
            }

            if (currentUser.Role != Role.Admin)
            {
                _logger.LogError("Usuário {UserId} sem permissão de admin tentou aprovar parceiro.", currentUser.Id);
                return Result.Failure(Error.Forbidden());
            }

            var partner = await _dbContext.LegalPeople
                .FirstOrDefaultAsync(lp => lp.Id == request.PartnerId, ct);

            if (partner is null)
            {
                _logger.LogError("Parceiro {PartnerId} não encontrado.", request.PartnerId);
                return Result.Failure(Error.NotFound($"Parceiro {request.PartnerId} não encontrado."));
            }

            if (partner.PersonStatus != PersonStatus.PendingApproval)
            {
                _logger.LogError("Status inválido para aprovação: {Status}", partner.PersonStatus);
                return Result.Failure(Error.Conflict(
                    $"Não é possível aprovar um parceiro com status '{partner.PersonStatus}'."));
            }

            partner.Approve(currentUser.Role);
            await _dbContext.SaveChangesAsync(ct);

            await _emailSender.SendAsync(partner.Email, EmailType.PartnerApproval, ct: ct);

            _logger.LogInformation("Parceiro {PartnerId} aprovado pelo admin {AdminId}.", request.PartnerId, currentUser.Id);
            return Result.Success();
        }
    }
}

public class ApprovePartnerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/admin/partners/{id}/approve",
            async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new ApprovePartner.Command { PartnerId = id });
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .WithTags("Admin")
            .WithName("ApprovePartner")
            .WithSummary("Aprova o cadastro de um parceiro PJ (PendingApproval → Active).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

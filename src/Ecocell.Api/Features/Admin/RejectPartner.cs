using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests.Admin;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Admin.RejectPartner;

namespace Ecocell.Api.Features.Admin;

public static class RejectPartner
{
    public record Command : IRequest<Result>
    {
        public Guid PartnerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PartnerId).NotEmpty().WithMessage("PartnerId é obrigatório.");
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Motivo da rejeição é obrigatório.")
                .MaximumLength(500).WithMessage("Motivo deve ter no máximo 500 caracteres.");
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
                _logger.LogError("Usuário não autenticado ou inativo tentou rejeitar parceiro.");
                return Result.Failure(Error.Forbidden());
            }

            if (currentUser.Role != Role.Admin)
            {
                _logger.LogError("Usuário {UserId} sem permissão de admin tentou rejeitar parceiro.", currentUser.Id);
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
                _logger.LogError("Status inválido para rejeição: {Status}", partner.PersonStatus);
                return Result.Failure(Error.Conflict(
                    $"Não é possível rejeitar um parceiro com status '{partner.PersonStatus}'."));
            }

            partner.Reject(currentUser.Role);
            await _dbContext.SaveChangesAsync(ct);

            await _emailSender.SendPartnerRejectionAsync(partner.Email, request.Reason, ct);

            _logger.LogInformation("Parceiro {PartnerId} rejeitado pelo admin {AdminId}. Motivo: {Reason}",
                request.PartnerId, currentUser.Id, request.Reason);
            return Result.Success();
        }
    }
}

public class RejectPartnerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/admin/partners/{id}/reject",
            async (Guid id, [FromBody] RequestRejectPartner req, ISender sender) =>
            {
                var result = await sender.Send(new RejectPartner.Command
                {
                    PartnerId = id,
                    Reason = req.Reason
                });
                return result.ToProcessResult(StatusCodes.Status200OK);
            })
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .WithTags("Admin")
            .WithName("RejectPartner")
            .WithSummary("Rejeita o cadastro de um parceiro PJ com motivo obrigatório (RN008).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}
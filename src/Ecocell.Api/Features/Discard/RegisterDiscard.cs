using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests.Discards;
using Ecocell.Shared.Responses;
using Ecocell.Shared.Utils;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecocell.Api.Features.Discard;

public static class RegisterDiscard
{
    public sealed record ItemCommand
    {
        public ElectronicMaterial Material { get; init; }
        public int Quantity { get; set; }
        public decimal ApproximateWeightKg { get; set; }
    }

    public sealed record Command : IRequest<ResultT<ResponseRegisterDiscardJson>>
    {
        public string QrCode { get; init; } = string.Empty;
        public IReadOnlyList<ItemCommand> Items { get; init; } = [];
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(value => value.QrCode)
                .NotEmpty()
                .Must(value => CollectorPointQrCode.TryParse(value, out _))
                .WithMessage("O QR Code do Ponto de Coleta é inválido.");

            RuleFor(value => value.Items)
                .NotEmpty()
                .WithMessage("Informe ao menos um item.");

            RuleFor(value => value.Items)
                .Must(items => items is not null
                    && items.All(item => item is not null)
                    && items.Select(item => item!.Material).Distinct().Count() == items.Count)
                .WithMessage("Cada material pode aparecer somente uma vez.");

            RuleForEach(value => value.Items)
                .NotNull()
                .WithMessage("O item do descarte é obrigatório.");

            RuleForEach(value => value.Items).ChildRules(item =>
            {
                item.RuleFor(value => value.Material)
                    .IsInEnum()
                    .Must(value => Convert.ToInt32(value) > 0)
                    .WithMessage("O material é inválido.");
                item.RuleFor(value => value.Quantity)
                    .GreaterThan(0)
                    .WithMessage("A quantidade deve ser maior que zero.");
                item.RuleFor(value => value.ApproximateWeightKg)
                    .GreaterThan(0)
                    .WithMessage("O peso aproximado deve ser maior que zero.")
                    .PrecisionScale(10, 3, false)
                    .WithMessage("O peso aproximado deve ter até 10 dígitos totais e 3 casas decimais.");
            });
        }
    }

    public sealed class Handler : IRequestHandler<Command, ResultT<ResponseRegisterDiscardJson>>
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

        public async ValueTask<ResultT<ResponseRegisterDiscardJson>> Handle(
            Command request,
            CancellationToken ct)
        {
            var validation = await _validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return ResultT<ResponseRegisterDiscardJson>.Failure(
                    Error.ErrorOnValidation(
                        validation.Errors.Select(value => value.ErrorMessage).ToList()));
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync(ct);
            if (currentUser is null
                || currentUser.PersonType != PersonType.NaturalPerson
                || currentUser.PersonStatus != PersonStatus.Active
                || currentUser.Journey != Journey.Depositor)
            {
                return ResultT<ResponseRegisterDiscardJson>.Failure(Error.Forbidden());
            }

            CollectorPointQrCode.TryParse(request.QrCode, out var collectorPointId);

            var collectorPoint = await _dbContext.LegalPeople
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    value => value.Id == collectorPointId
                        && value.Journey == Journey.CollectPoint,
                    ct);

            if (collectorPoint is null)
            {
                return ResultT<ResponseRegisterDiscardJson>.Failure(
                    Error.NotFound("Ponto de Coleta não encontrado."));
            }

            if (collectorPoint.PersonStatus != PersonStatus.Active)
            {
                return ResultT<ResponseRegisterDiscardJson>.Failure(
                    Error.Conflict("Ponto de Coleta não está ativo."));
            }

            var openedAt = DateTime.UtcNow;
            var materials = request.Items.Select(value => value.Material).ToArray();

            var rules = await _dbContext.MaterialScoreRules
                .AsNoTracking()
                .Where(value => value.LegalPersonId == collectorPoint.Id
                    && materials.Contains(value.Material)
                    && value.ValidFrom <= openedAt
                    && (value.ValidTo == null || value.ValidTo > openedAt))
                .ToListAsync(ct);

            var rulesByMaterial = rules.ToDictionary(value => value.Material);
            var unsupported = materials
                .Where(value => !rulesByMaterial.ContainsKey(value))
                .Distinct()
                .Order()
                .ToArray();

            if (unsupported.Length > 0)
            {
                return ResultT<ResponseRegisterDiscardJson>.Failure(
                    Error.Conflict(
                        $"Materiais não aceitos pelo Ponto de Coleta: {string.Join(", ", unsupported)}."));
            }

            var items = request.Items
                .Select(value => new DiscardItem(
                    value.Material,
                    value.Quantity,
                    value.ApproximateWeightKg,
                    rulesByMaterial[value.Material].Id))
                .ToArray();

            var discard = new Ecocell.Api.Entities.Discard(
                currentUser.Id,
                collectorPoint.Id,
                items);

            _dbContext.Discards.Add(discard);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Descarte {DiscardId} aberto pelo depositante {DepositorId} no ponto {CollectorPointId}.",
                discard.Id,
                currentUser.Id,
                collectorPoint.Id);

            return ResultT<ResponseRegisterDiscardJson>.Success(
                new ResponseRegisterDiscardJson
                {
                    Id = discard.Id,
                    Status = (Ecocell.Shared.Enums.DiscardStatus)(int)discard.Status,
                    CreatedAt = discard.CreatedAt,
                });
        }
    }

}

public sealed class RegisterDiscardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "api/v1/discards",
            async (
                RequestRegisterDiscardJson request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new RegisterDiscard.Command
                {
                    QrCode = request.QrCode,
                    Items = (request.Items ?? [])
                        .Select(item => item is null
                            ? null!
                            : new RegisterDiscard.ItemCommand
                            {
                                Material = (ElectronicMaterial)(int)item.Material,
                                Quantity = item.Quantity,
                                ApproximateWeightKg = item.ApproximateWeightKg,
                            })
                        .ToArray(),
                };

                var result = await sender.Send(command, ct);
                return result.ToProcessResult(StatusCodes.Status201Created);
            })
            .WithTags("Discard")
            .WithName("RegisterDiscard")
            .WithSummary("Abre um descarte pendente a partir do QR Code de um Ponto de Coleta.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces<ResponseRegisterDiscardJson>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

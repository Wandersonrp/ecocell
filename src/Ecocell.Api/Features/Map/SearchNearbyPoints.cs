using Ecocell.Api.Database;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses;
using FluentValidation;
using Mediator;

namespace Ecocell.Api.Features.Map;

public static class SearchNearbyPoints
{
    public const double DefaultRadiusKm = 10;
    public const double MinRadiusKm = 1;
    public const double MaxRadiusKm = 50;

    public record Command : IRequest<ResultT<IReadOnlyList<ResponseNearbyPoint>>>
    {
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public double? RadiusKm { get; init; }
        public string? City { get; init; }

        public bool IsProximityMode => Latitude.HasValue && Longitude.HasValue;
        public bool IsCityMode => !string.IsNullOrWhiteSpace(City);
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x)
                .Must(c => c.IsProximityMode ^ c.IsCityMode)
                .WithMessage("Informe coordenadas (latitude e longitude) ou uma cidade, mas não ambos.");

            When(x => x.Latitude.HasValue || x.Longitude.HasValue, () =>
            {
                RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90m, 90m)
                    .WithMessage("Latitude deve estar entre -90 e 90.");
                RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180m, 180m)
                    .WithMessage("Longitude deve estar entre -180 e 180.");
            });

            When(x => x.RadiusKm.HasValue, () =>
            {
                RuleFor(x => x.RadiusKm!.Value).InclusiveBetween(MinRadiusKm, MaxRadiusKm)
                    .WithMessage($"O raio deve estar entre {MinRadiusKm} e {MaxRadiusKm} km.");
            });
        }
    }

    public sealed class Handler : IRequestHandler<Command, ResultT<IReadOnlyList<ResponseNearbyPoint>>>
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

        public ValueTask<ResultT<IReadOnlyList<ResponseNearbyPoint>>> Handle(Command request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

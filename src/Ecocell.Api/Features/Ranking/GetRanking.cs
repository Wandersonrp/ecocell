using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Ecocell.Shared.Responses.Ranking;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RankingScope = Ecocell.Shared.Enums.RankingScope;

namespace Ecocell.Api.Features.Ranking;

public static class GetRanking
{
    public sealed record Query : IRequest<ResultT<ResponseRankingJson>>
    {
        public RankingScope? Scope { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Scope).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("O escopo do ranking é obrigatório.")
                .Must(scope => Enum.IsDefined(scope!.Value)).WithMessage("O escopo do ranking é inválido.");
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1).WithMessage("Page deve ser maior ou igual a 1.");
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize deve ser entre 1 e 100.");
            When(query => query.Scope == RankingScope.Municipal, () =>
            {
                RuleFor(query => query.City).Must(city => !string.IsNullOrWhiteSpace(city)).WithMessage("City é obrigatória no ranking Municipal.");
                RuleFor(query => query.State).Must(state => !string.IsNullOrWhiteSpace(state)).WithMessage("State é obrigatória no ranking Municipal.");
            });
            When(query => query.Page > 0 && query.PageSize > 0, () =>
                RuleFor(query => query).Must(query => ((long)query.Page - 1L) * query.PageSize <= int.MaxValue)
                    .WithMessage("A página solicitada excede o limite suportado."));
        }
    }

    public sealed class Handler(AppDbContext dbContext, IValidator<Query> validator, ICurrentUserService currentUserService)
        : IRequestHandler<Query, ResultT<ResponseRankingJson>>
    {
        public async ValueTask<ResultT<ResponseRankingJson>> Handle(Query request, CancellationToken cancellationToken)
        {
            var validation = validator.Validate(request);
            if (!validation.IsValid)
                return ResultT<ResponseRankingJson>.Failure(Error.ErrorOnValidation(validation.Errors.Select(error => error.ErrorMessage).ToList()));

            var currentUser = await currentUserService.GetCurrentUserAsync(cancellationToken);
            if (currentUser is null)
                return ResultT<ResponseRankingJson>.Failure(Error.Unauthorized());

            if (currentUser.Role != Role.User || currentUser.PersonType != PersonType.NaturalPerson
                || currentUser.Journey != Journey.Depositor || currentUser.PersonStatus != PersonStatus.Active)
                return ResultT<ResponseRankingJson>.Failure(Error.Forbidden());

            var scope = request.Scope!.Value.ToString();
            var ranking = dbContext.Set<DepositorRankingRow>().AsNoTracking().Where(row => row.Scope == scope);
            if (request.Scope == RankingScope.Municipal)
            {
                var city = request.City!.Trim().ToLowerInvariant();
                var state = request.State!.Trim().ToUpperInvariant();
                ranking = ranking.Where(row => row.City == city && row.State == state);
            }

            var offset = checked((int)(((long)request.Page - 1L) * request.PageSize));
            var rows = await ranking.OrderBy(row => row.Position).ThenBy(row => row.FullName).ThenBy(row => row.DepositorId)
                .Skip(offset).Take(request.PageSize + 1).ToListAsync(cancellationToken);
            var hasMore = rows.Count > request.PageSize;
            if (hasMore) rows.RemoveAt(rows.Count - 1);
            var currentRow = rows.FirstOrDefault(row => row.DepositorId == currentUser.Id)
                ?? await ranking.FirstOrDefaultAsync(row => row.DepositorId == currentUser.Id, cancellationToken);

            return ResultT<ResponseRankingJson>.Success(new ResponseRankingJson
            {
                Items = rows.Select(row => ToResponse(row, currentUser.Id)).ToList(),
                CurrentUser = currentRow is null ? null : ToResponse(currentRow, currentUser.Id),
                Page = request.Page,
                PageSize = request.PageSize,
                HasMore = hasMore
            });
        }
    }

    internal static string ReduceName(string fullName)
    {
        var parts = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch { 0 => string.Empty, 1 => parts[0], _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}." };
    }

    private static ResponseRankingItemJson ToResponse(DepositorRankingRow row, Guid currentUserId) => new()
    {
        ReducedName = ReduceName(row.FullName), Position = row.Position, Points = row.TotalPoints, IsCurrentUser = row.DepositorId == currentUserId
    };
}

public sealed class DepositorRankingRow
{
    public string Scope { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? City { get; set; }
    public Guid DepositorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
    public long Position { get; set; }
}

public sealed class DepositorRankingRowConfiguration : IEntityTypeConfiguration<DepositorRankingRow>
{
    public void Configure(EntityTypeBuilder<DepositorRankingRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_ranking_depositor");
        builder.Property(row => row.TotalPoints).HasPrecision(28, 5);
    }
}

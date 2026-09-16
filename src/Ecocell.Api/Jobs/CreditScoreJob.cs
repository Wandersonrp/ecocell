using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Jobs;

public sealed class CreditScoreJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CreditScoreJob> _logger;
    private readonly TimeProvider _timeProvider;

    public CreditScoreJob(
        AppDbContext dbContext,
        ILogger<CreditScoreJob> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        Guid creditScoreRequestId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var request = await _dbContext.CreditScoreRequests
            .Include(value => value.Discard)
                .ThenInclude(value => value.Depositor)
            .Include(value => value.Discard)
                .ThenInclude(value => value.Items)
                    .ThenInclude(value => value.MaterialScoreRule)
            .SingleOrDefaultAsync(value => value.Id == creditScoreRequestId, cancellationToken);

        if (request is null || request.DispatchedAt is not null)
            return;

        if (request.Discard.Status != DiscardStatus.Confirmed)
        {
            _logger.LogError(
                "Solicitação {CreditScoreRequestId} referencia descarte não confirmado {DiscardId}.",
                request.Id,
                request.DiscardId);
            throw new InvalidOperationException(
                "Somente descartes confirmados podem gerar crédito de pontuação.");
        }

        var dispatchedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var transactionExists = await _dbContext.DepositorScoreTransactions.AnyAsync(
            value => value.DiscardId == request.DiscardId,
            cancellationToken);
        if (transactionExists)
        {
            request.MarkAsDispatched(dispatchedAt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Solicitação {CreditScoreRequestId} recuperada para descarte já creditado {DiscardId}.",
                request.Id,
                request.DiscardId);
            return;
        }

        if (request.Discard.Depositor.Role is Role.Admin or Role.Support)
        {
            request.MarkAsDispatched(dispatchedAt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Crédito suprimido pela RN013. Solicitação: {CreditScoreRequestId}; descarte: {DiscardId}; role: {Role}.",
                request.Id,
                request.DiscardId,
                request.Discard.Depositor.Role);
            return;
        }

        var points = request.Discard.Items.Sum(CalculatePoints);
        var total = await _dbContext.DepositorTotalScores.SingleOrDefaultAsync(
            value => value.DepositorId == request.Discard.DepositorId,
            cancellationToken);

        _dbContext.DepositorScoreTransactions.Add(new DepositorScoreTransaction(
            request.DiscardId,
            request.Discard.DepositorId,
            points));

        if (total is null)
        {
            _dbContext.DepositorTotalScores.Add(new DepositorTotalScore(
                request.Discard.DepositorId,
                points));
        }
        else
        {
            total.Credit(points);
        }

        request.MarkAsDispatched(dispatchedAt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Crédito processado. Solicitação: {CreditScoreRequestId}; descarte: {DiscardId}; pontos: {Points}.",
            request.Id,
            request.DiscardId,
            points);
    }

    private static decimal CalculatePoints(DiscardItem item) =>
        item.MaterialScoreRule.Unit switch
        {
            MaterialScoreUnit.PerUnit => item.MaterialScoreRule.Points * item.Quantity,
            MaterialScoreUnit.PerKilogram =>
                item.MaterialScoreRule.Points * item.ApproximateWeightKg,
            _ => throw new InvalidOperationException("A unidade de pontuação é inválida."),
        };
}

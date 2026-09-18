using Ecocell.Api.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecocell.Api.Jobs;

internal sealed class CreditScoreDispatcher : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;
    private static readonly string[] RetryableUniqueConstraints =
    [
        "IX_DepositorScoreTransactions_DiscardId",
        "IX_DepositorTotalScores_DepositorId",
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CreditScoreDispatcher> _logger;

    public CreditScoreDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<CreditScoreDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await DispatchCycleAsync(stoppingToken);
            using var timer = new PeriodicTimer(PollingInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await DispatchCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }

    private async Task DispatchCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DispatchPendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha no ciclo de processamento de créditos; novo ciclo tentará novamente.");
        }
    }

    internal async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        List<PendingRequest> pending;
        await using (var queryScope = _scopeFactory.CreateAsyncScope())
        {
            var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
            pending = await db.CreditScoreRequests
                .AsNoTracking()
                .Where(value => value.DispatchedAt == null)
                .OrderBy(value => value.CreatedAt)
                .ThenBy(value => value.Id)
                .Take(BatchSize)
                .Select(value => new PendingRequest(value.Id, value.DiscardId))
                .ToListAsync(cancellationToken);
        }

        foreach (var request in pending)
        {
            try
            {
                await using var processingScope = _scopeFactory.CreateAsyncScope();
                var job = processingScope.ServiceProvider.GetRequiredService<CreditScoreJob>();
                await job.ExecuteAsync(request.Id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                LogRetryableConflict(exception, request);
            }
            catch (DbUpdateException exception) when (IsRetryableUniqueConflict(exception))
            {
                LogRetryableConflict(exception, request);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Falha ao processar solicitação {CreditScoreRequestId} do descarte {DiscardId}; pedido permanecerá pendente.",
                    request.Id,
                    request.DiscardId);
            }
        }
    }

    private void LogRetryableConflict(Exception exception, PendingRequest request) =>
        _logger.LogWarning(
            exception,
            "Conflito ao processar solicitação {CreditScoreRequestId} do descarte {DiscardId}; novo ciclo tentará novamente.",
            request.Id,
            request.DiscardId);

    private static bool IsRetryableUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && RetryableUniqueConstraints.Contains(postgres.ConstraintName);

    private sealed record PendingRequest(Guid Id, Guid DiscardId);
}

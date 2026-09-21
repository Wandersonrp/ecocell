using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecocell.IntegrationTests.Infrastructure;

internal sealed class CreditScoreWriteBarrierInterceptor(string commandMarker)
    : DbCommandInterceptor
{
    private readonly TaskCompletionSource<bool> _bothArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivalCount;

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await WaitForBothWritersAsync(command.CommandText, cancellationToken);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await WaitForBothWritersAsync(command.CommandText, cancellationToken);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task WaitForBothWritersAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        if (!commandText.Contains(commandMarker, StringComparison.Ordinal))
            return;

        if (Interlocked.Increment(ref _arrivalCount) == 2)
            _bothArrived.TrySetResult(true);

        await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}

using Ecocell.Api.Database;
using Ecocell.Api.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Ecocell.UnitTests.Jobs;

public class CreditScoreDispatcherTests
{
    [Fact]
    public async Task StartAsync_ShouldNotFault_WhenBatchScopeCannotResolveDbContext()
    {
        var scopeFactory = new FailingScopeFactory();
        var dispatcher = new CreditScoreDispatcher(
            scopeFactory,
            NullLogger<CreditScoreDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        await scopeFactory.DbContextResolution.Task;
        await dispatcher.StopAsync(CancellationToken.None);

        dispatcher.ExecuteTask.ShouldNotBeNull();
        dispatcher.ExecuteTask.IsFaulted.ShouldBeFalse();
    }

    private sealed class FailingScopeFactory : IServiceScopeFactory
    {
        private readonly FailingServiceProvider _provider = new();

        public TaskCompletionSource<bool> DbContextResolution => _provider.DbContextResolution;

        public IServiceScope CreateScope() => new FailingScope(_provider);
    }

    private sealed class FailingServiceProvider : IServiceProvider
    {
        public TaskCompletionSource<bool> DbContextResolution { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(AppDbContext))
                DbContextResolution.TrySetResult(true);

            return null;
        }
    }

    private sealed class FailingScope : IServiceScope
    {
        public FailingScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }
}

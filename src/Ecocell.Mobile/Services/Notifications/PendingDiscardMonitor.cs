using System.Text.Json;
using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Auth;
using Ecocell.Mobile.Services.Context;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.EventArgs;

namespace Ecocell.Mobile.Services.Notifications;

public sealed class PendingDiscardMonitor : IAsyncDisposable
{
    private const string ExplainedKey = "ecocell.notifications.explained";
    private readonly IDiscardClient _discardClient;
    private readonly AuthStateService _authState;
    private readonly ActiveContextService _context;
    private CancellationTokenSource? _loopCts;
    private Task _loopTask = Task.CompletedTask;
    private int _refreshSequence;
    private Guid? _notificationTarget;
    private bool _isForeground;
    private bool _disposed;

    public PendingDiscardMonitor(
        IDiscardClient discardClient,
        AuthStateService authState,
        ActiveContextService context)
    {
        _discardClient = discardClient;
        _authState = authState;
        _context = context;

        _authState.AuthStateChanged += Restart;
        _context.ContextChanged += Restart;
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;

        var launchDetails = LocalNotificationCenter.LaunchNotificationDetails;
        if (launchDetails?.DidNotificationLaunchApp == true)
            CaptureNotificationTarget(launchDetails.Request?.ReturningData);
    }

    public event Action? Changed;
    public event Action? NotificationTargetAvailable;

    public int PendingCount { get; private set; }
    public bool IsLoading { get; private set; }
    public bool ShouldExplainNotifications =>
        !Preferences.Default.Get(ExplainedKey, false);

    private bool IsEligible =>
        _isForeground
        && _authState.IsAuthenticated
        && _context.Current is { IsCollectPoint: true, CollectPointId: not null };

    public void SetForeground(bool isForeground)
    {
        if (_isForeground == isForeground)
            return;

        _isForeground = isForeground;
        Restart();
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!IsEligible)
            return;

        var pointId = _context.Current.CollectPointId!.Value;
        var generation = _context.Generation;
        var refreshSequence = Interlocked.Increment(ref _refreshSequence);
        IsLoading = true;
        Changed?.Invoke();

        try
        {
            var response = await _discardClient.ListPendingAsync(pointId, ct);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return;

            if (!IsCurrent(pointId, generation, ct))
                return;

            var currentIds = response.Content.Items.Select(value => value.Id).ToHashSet();
            var hasBaseline = Preferences.Default.Get(BaselineKey(pointId), false);
            var knownIds = ReadKnownIds(pointId);
            var newCount = hasBaseline ? currentIds.Except(knownIds).Count() : 0;

            if (newCount > 0)
            {
                var request = new NotificationRequest
                {
                    NotificationId = BitConverter.ToInt32(pointId.ToByteArray(), 0) & int.MaxValue,
                    Title = "Novos descartes pendentes",
                    Description = newCount == 1
                        ? "Há 1 novo descarte aguardando conferência."
                        : $"Há {newCount} novos descartes aguardando conferência.",
                    ReturningData = pointId.ToString("D"),
                };

                await LocalNotificationCenter.Current.Show(request);
                if (!IsCurrent(pointId, generation, ct))
                    return;
            }

            PendingCount = currentIds.Count;
            WriteKnownIds(pointId, currentIds);
            Preferences.Default.Set(BaselineKey(pointId), true);
            Changed?.Invoke();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch
        {
            // A failed request must not change the current count or persisted baseline.
        }
        finally
        {
            if (refreshSequence == Volatile.Read(ref _refreshSequence))
            {
                IsLoading = false;
                Changed?.Invoke();
            }
        }
    }

    public async Task<bool> EnableNotificationsAsync()
    {
        Preferences.Default.Set(ExplainedKey, true);
        return await LocalNotificationCenter.Current.RequestNotificationPermission(
            new NotificationPermission());
    }

    public void DismissNotificationPrompt() => Preferences.Default.Set(ExplainedKey, true);

    public bool TryTakeNotificationTarget(out Guid collectorPointId)
    {
        if (_notificationTarget is not { } target)
        {
            collectorPointId = default;
            return false;
        }

        _notificationTarget = null;
        collectorPointId = target;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _authState.AuthStateChanged -= Restart;
        _context.ContextChanged -= Restart;
        LocalNotificationCenter.Current.NotificationActionTapped -= OnNotificationActionTapped;
        _loopCts?.Cancel();
        _loopCts?.Dispose();

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Restart()
    {
        if (_disposed)
            return;

        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;

        if (!IsEligible)
        {
            IsLoading = false;
            return;
        }

        _loopCts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCts.Token);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await RefreshAsync(ct);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(ct))
                await RefreshAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private bool IsCurrent(Guid pointId, int generation, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && IsEligible
        && _context.Generation == generation
        && _context.Current.CollectPointId == pointId;

    private void OnNotificationActionTapped(NotificationActionEventArgs args) =>
        CaptureNotificationTarget(args.Request?.ReturningData);

    private void CaptureNotificationTarget(string? returningData)
    {
        if (!Guid.TryParseExact(returningData, "D", out var id))
            return;

        _notificationTarget = id;
        NotificationTargetAvailable?.Invoke();
    }

    private static string BaselineKey(Guid id) => $"ecocell.pending.{id:D}.baseline";
    private static string KnownIdsKey(Guid id) => $"ecocell.pending.{id:D}.ids";

    private static HashSet<Guid> ReadKnownIds(Guid id)
    {
        var json = Preferences.Default.Get(KnownIdsKey(id), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<HashSet<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void WriteKnownIds(Guid id, IEnumerable<Guid> ids) =>
        Preferences.Default.Set(KnownIdsKey(id), JsonSerializer.Serialize(ids));
}

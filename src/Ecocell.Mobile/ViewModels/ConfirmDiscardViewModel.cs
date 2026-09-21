using System.Net;
using Ecocell.Mobile.Models.Discards;
using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Context;
using Ecocell.Mobile.Services.Notifications;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests.Discards;
using Ecocell.Shared.Responses.Discards;

namespace Ecocell.Mobile.ViewModels;

public enum ConfirmState
{
    Loading,
    List,
    Detail,
    Submitting,
    Empty,
    Error,
}

public sealed class ConfirmDiscardViewModel : IDisposable
{
    private readonly IDiscardClient _client;
    private readonly ActiveContextService _context;
    private readonly PendingDiscardMonitor _monitor;
    private CancellationTokenSource _contextCts = new();
    private bool _disposed;

    public ConfirmDiscardViewModel(
        IDiscardClient client,
        ActiveContextService context,
        PendingDiscardMonitor monitor)
    {
        _client = client;
        _context = context;
        _monitor = monitor;
        _context.ContextChanged += OnContextChanged;
    }

    public ConfirmState State { get; private set; } = ConfirmState.Loading;
    public IList<ResponsePendingDiscardJson> Items { get; } = [];
    public ResponsePendingDiscardJson? Selected { get; private set; }
    public IList<DiscardItemDraft> DraftItems { get; } = [];
    public string? ErrorMessage { get; private set; }
    public string? FeedbackMessage { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var current = _context.Current;
        if (!current.IsCollectPoint || current.CollectPointId is null)
        {
            State = ConfirmState.Error;
            ErrorMessage = "Selecione um ponto de coleta para consultar os descartes.";
            Changed?.Invoke();
            return;
        }

        var collectorPointId = current.CollectPointId.Value;
        var generation = _context.Generation;
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _contextCts.Token);
        var operationToken = operationCts.Token;
        State = ConfirmState.Loading;
        Selected = null;
        DraftItems.Clear();
        ErrorMessage = null;
        Changed?.Invoke();

        try
        {
            var response = await _client.ListPendingAsync(collectorPointId, operationToken);
            if (!IsCurrent(collectorPointId, generation) || operationToken.IsCancellationRequested)
                return;

            Items.Clear();
            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                foreach (var item in response.Content.Items.OrderBy(item => item.CreatedAt))
                    Items.Add(item);

                State = Items.Count == 0 ? ConfirmState.Empty : ConfirmState.List;
                Changed?.Invoke();
                return;
            }

            State = ConfirmState.Error;
            ErrorMessage = "Não foi possível carregar os descartes pendentes. Tente novamente.";
            Changed?.Invoke();
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            if (IsCurrent(collectorPointId, generation) && !ct.IsCancellationRequested)
            {
                State = Items.Count == 0 ? ConfirmState.Empty : ConfirmState.List;
                Changed?.Invoke();
            }
        }
        catch
        {
            if (!IsCurrent(collectorPointId, generation))
                return;

            State = ConfirmState.Error;
            ErrorMessage = "Falha de conexão. Verifique sua internet e tente novamente.";
            Changed?.Invoke();
        }
    }

    public void Open(ResponsePendingDiscardJson discard)
    {
        if (!_context.Current.IsCollectPoint || !Items.Contains(discard))
            return;

        Selected = discard;
        DraftItems.Clear();
        foreach (var item in discard.Items)
        {
            DraftItems.Add(new DiscardItemDraft
            {
                Material = item.Material,
                QuantityText = item.Quantity.ToString(),
                WeightText = item.ApproximateWeightKg.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        ErrorMessage = null;
        FeedbackMessage = null;
        State = ConfirmState.Detail;
    }

    public void BackToList()
    {
        Selected = null;
        DraftItems.Clear();
        ErrorMessage = null;
        State = Items.Count == 0 ? ConfirmState.Empty : ConfirmState.List;
    }

    public void AddItem() => DraftItems.Add(new DiscardItemDraft());

    public void RemoveItem(DiscardItemDraft item) => DraftItems.Remove(item);

    public async Task ConfirmAsync(CancellationToken ct = default)
    {
        if (Selected is null)
            return;

        var values = new List<(ElectronicMaterial Material, int Quantity, decimal ApproximateWeightKg)>();
        var selectedMaterials = DraftItems
            .Where(item => item.Material is not null)
            .Select(item => item.Material!.Value)
            .ToArray();

        foreach (var item in DraftItems)
        {
            var isDuplicated = item.Material is not null
                && selectedMaterials.Count(material => material == item.Material.Value) > 1;
            if (!item.TryGetValues(isDuplicated, out var material, out var quantity, out var approximateWeightKg))
                continue;

            values.Add((material, quantity, approximateWeightKg));
        }

        if (DraftItems.Count == 0 || values.Count != DraftItems.Count)
        {
            ErrorMessage = "Revise os itens antes de confirmar o descarte.";
            State = ConfirmState.Detail;
            return;
        }

        var request = new RequestConfirmDiscardJson
        {
            Items = values.Select(value => new RequestConfirmDiscardItemJson
            {
                Material = value.Material,
                Quantity = value.Quantity,
                ApproximateWeightKg = value.ApproximateWeightKg,
            }).ToArray(),
        };

        await ProcessAsync(
            token => _client.ConfirmAsync(Selected.Id, request, token),
            ct);
    }

    public async Task RejectAsync(CancellationToken ct = default)
    {
        if (Selected is null)
            return;

        await ProcessAsync(token => _client.RejectAsync(Selected.Id, token), ct);
    }

    private async Task ProcessAsync(
        Func<CancellationToken, Task<Refit.IApiResponse>> request,
        CancellationToken ct)
    {
        var selected = Selected;
        var current = _context.Current;
        if (selected is null
            || !Items.Contains(selected)
            || !current.IsCollectPoint
            || current.CollectPointId is null)
            return;

        var collectorPointId = current.CollectPointId.Value;
        var generation = _context.Generation;
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _contextCts.Token);
        var operationToken = operationCts.Token;
        if (!IsCurrent(collectorPointId, generation) || operationToken.IsCancellationRequested)
            return;

        State = ConfirmState.Submitting;
        ErrorMessage = null;
        Changed?.Invoke();

        try
        {
            var response = await request(operationToken);
            if (!IsCurrent(collectorPointId, generation) || operationToken.IsCancellationRequested)
                return;

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                Items.Remove(selected);
                Selected = null;
                DraftItems.Clear();
                State = Items.Count == 0 ? ConfirmState.Empty : ConfirmState.List;
                Changed?.Invoke();
                await _monitor.RefreshAsync(operationToken);
                return;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                FeedbackMessage = "Este descarte já foi processado.";
                await LoadAsync(operationToken);
                return;
            }

            State = ConfirmState.Detail;
            ErrorMessage = "Não foi possível processar o descarte. Tente novamente.";
            Changed?.Invoke();
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            if (IsCurrent(collectorPointId, generation) && !ct.IsCancellationRequested)
            {
                State = ConfirmState.Detail;
                Changed?.Invoke();
            }
        }
        catch
        {
            if (!IsCurrent(collectorPointId, generation))
                return;

            State = ConfirmState.Detail;
            ErrorMessage = "Falha de conexão. Verifique sua internet e tente novamente.";
            Changed?.Invoke();
        }
    }

    private void OnContextChanged()
    {
        var previousCts = Interlocked.Exchange(ref _contextCts, new CancellationTokenSource());
        previousCts.Cancel();
        previousCts.Dispose();

        Selected = null;
        Items.Clear();
        DraftItems.Clear();
        ErrorMessage = null;
        FeedbackMessage = null;
        State = _context.Current.IsCollectPoint
            ? ConfirmState.Loading
            : ConfirmState.Error;
        Changed?.Invoke();

        if (!_disposed && _context.Current.IsCollectPoint)
            _ = LoadAsync(_contextCts.Token);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _context.ContextChanged -= OnContextChanged;
        var cts = _contextCts;
        cts.Cancel();
        cts.Dispose();
    }

    private bool IsCurrent(Guid collectorPointId, int generation) =>
        !_disposed
        && _context.Generation == generation
        && _context.Current.IsCollectPoint
        && _context.Current.CollectPointId == collectorPointId;
}

using System.Net;
using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Context;

namespace Ecocell.Mobile.ViewModels;

public sealed class MyQrCodeViewModel
{
    private readonly ICollectorPointClient _client;
    private readonly ActiveContextService _context;

    public MyQrCodeViewModel(ICollectorPointClient client, ActiveContextService context)
    {
        _client = client;
        _context = context;
    }

    public enum QrState { Loading, Active, Unavailable, Error }

    public QrState State { get; private set; } = QrState.Loading;
    public string? Qr { get; private set; }
    public string? PointName { get; private set; }

    /// <summary>
    /// Busca a string do QR do ponto ativo. Descarta a resposta se o contexto for
    /// trocado durante o request (usa <see cref="ActiveContextService.Generation"/>).
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        State = QrState.Loading;
        Qr = null;

        var current = _context.Current;
        PointName = current.DisplayName;

        if (!current.IsCollectPoint || current.CollectPointId is null)
        {
            State = QrState.Error;
            return;
        }

        var id = current.CollectPointId.Value;
        var generation = _context.Generation;

        try
        {
            var response = await _client.GetQrCodeAsync(id, ct);

            if (_context.Generation != generation)
                return; // contexto mudou; resposta obsoleta

            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                Qr = response.Content.Qr;
                State = QrState.Active;
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                State = QrState.Unavailable;
            }
            else
            {
                State = QrState.Error;
            }
        }
        catch
        {
            if (_context.Generation != generation)
                return;
            State = QrState.Error;
        }
    }
}

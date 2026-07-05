using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Context;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Responses;

namespace Ecocell.Mobile.ViewModels;

public sealed class ManagedCollectorPointsViewModel
{
    private readonly ICollectorPointClient _client;
    private readonly ActiveContextService _context;

    public ManagedCollectorPointsViewModel(ICollectorPointClient client, ActiveContextService context)
    {
        _client = client;
        _context = context;
    }

    public IReadOnlyList<ResponseManagedCollectorPoint> Items { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public bool HasError { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var response = await _client.ListMineAsync(ct);
            if (response.IsSuccessStatusCode && response.Content is not null)
                Items = response.Content.Items;
            else
                HasError = true;
        }
        catch
        {
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Só PC Active pode ser acessado; Pending aparece mas não entra (RN007).</summary>
    public bool CanEnter(ResponseManagedCollectorPoint pc) => pc.Status == PersonStatus.Active;

    public void Enter(ResponseManagedCollectorPoint pc)
    {
        if (!CanEnter(pc)) return;
        _context.EnterCollectPoint(pc.Id, pc.TradeName);
    }
}

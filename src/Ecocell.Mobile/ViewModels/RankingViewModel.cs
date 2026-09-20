using Ecocell.Mobile.Services.Api;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests.Ranking;
using Ecocell.Shared.Responses.Ranking;

namespace Ecocell.Mobile.ViewModels;

public sealed class RankingViewModel
{
    private const string InitialError = "Não foi possível carregar o ranking. Tente novamente.";
    private readonly IRankingClient _client;

    public RankingViewModel(IRankingClient client) => _client = client;

    public const int PageSize = 20;

    public RankingScope Scope { get; private set; } = RankingScope.National;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public IReadOnlyList<ResponseRankingItemJson> Items { get; private set; } = [];
    public ResponseRankingItemJson? CurrentUser { get; private set; }
    public int Page { get; private set; }
    public bool HasMore { get; private set; }
    public bool IsInitialLoading { get; private set; }
    public bool IsLoadingMore { get; private set; }
    public string? InitialErrorMessage { get; private set; }
    public string? LoadMoreErrorMessage { get; private set; }
    public bool IsForbidden { get; private set; }
    public bool CanSearchMunicipal => !string.IsNullOrWhiteSpace(City) && !string.IsNullOrWhiteSpace(State);

    public Task InitializeAsync(CancellationToken ct = default) => LoadFirstPageAsync(RankingScope.National, ct);

    private async Task LoadFirstPageAsync(RankingScope scope, CancellationToken ct)
    {
        Scope = scope;
        City = string.Empty;
        State = string.Empty;
        Items = [];
        CurrentUser = null;
        Page = 0;
        HasMore = false;
        IsInitialLoading = true;
        InitialErrorMessage = null;

        try
        {
            var response = await _client.GetAsync(new RequestGetRankingJson
            {
                Scope = scope,
                Page = 1,
                PageSize = PageSize
            }, ct);

            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                Items = response.Content.Items;
                CurrentUser = response.Content.CurrentUser;
                Page = response.Content.Page;
                HasMore = response.Content.HasMore;
                return;
            }

            InitialErrorMessage = InitialError;
        }
        catch (Exception)
        {
            InitialErrorMessage = InitialError;
        }
        finally
        {
            IsInitialLoading = false;
        }
    }
}

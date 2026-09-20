using Ecocell.Mobile.Services.Api;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests.Ranking;
using Ecocell.Shared.Responses.Ranking;

namespace Ecocell.Mobile.ViewModels;

public sealed class RankingViewModel : IDisposable
{
    private const string InitialError = "Não foi possível carregar o ranking. Tente novamente.";
    private const string MunicipalError = "Revise a cidade e a UF e tente novamente.";
    private const string LoadMoreError = "Não foi possível carregar mais posições. Tente novamente.";
    private readonly IRankingClient _client;
    private CancellationTokenSource? _requestCancellation;
    private long _generation;

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
    public bool ShouldShowCurrentUserCard => CurrentUser is not null && !Items.Any(item => item.IsCurrentUser);

    public Task InitializeAsync(CancellationToken ct = default) => LoadFirstPageAsync(RankingScope.National, ct);

    public void SetCity(string city)
    {
        if (City == city)
            return;

        City = city;
        CancelActiveRequest();
    }

    public void SetState(string state)
    {
        if (State == state)
            return;

        State = state;
        CancelActiveRequest();
    }

    public Task SelectScopeAsync(RankingScope scope, CancellationToken ct = default)
    {
        if (scope == RankingScope.National)
            return LoadFirstPageAsync(scope, ct);

        CancelActiveRequest();
        Scope = scope;
        ClearResults();
        IsInitialLoading = false;
        IsLoadingMore = false;
        IsForbidden = false;
        return Task.CompletedTask;
    }

    public Task SearchMunicipalAsync(CancellationToken ct = default) =>
        Scope == RankingScope.Municipal && CanSearchMunicipal
            ? LoadFirstPageAsync(RankingScope.Municipal, ct)
            : Task.CompletedTask;

    public Task RetryInitialAsync(CancellationToken ct = default) =>
        Scope == RankingScope.National || CanSearchMunicipal
            ? LoadFirstPageAsync(Scope, ct)
            : Task.CompletedTask;

    public Task RetryLoadMoreAsync(CancellationToken ct = default) => LoadMoreAsync(ct);

    public async Task LoadMoreAsync(CancellationToken ct = default)
    {
        if (!HasMore || IsInitialLoading || IsLoadingMore || IsForbidden || Page == 0)
            return;

        var (generation, requestToken) = BeginRequest(ct);
        IsLoadingMore = true;
        LoadMoreErrorMessage = null;

        try
        {
            var response = await _client.GetAsync(CreateRequest(Page + 1), requestToken);
            if (!IsCurrent(generation))
                return;

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                SetForbidden();
                return;
            }

            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                Items = [.. Items, .. response.Content.Items];
                CurrentUser = response.Content.CurrentUser;
                Page = response.Content.Page;
                HasMore = response.Content.HasMore;
                return;
            }

            LoadMoreErrorMessage = LoadMoreError;
        }
        catch (OperationCanceledException) when (!IsCurrent(generation))
        {
        }
        catch (Exception) when (IsCurrent(generation))
        {
            LoadMoreErrorMessage = LoadMoreError;
        }
        finally
        {
            if (IsCurrent(generation))
                IsLoadingMore = false;
        }
    }

    private async Task LoadFirstPageAsync(RankingScope scope, CancellationToken ct)
    {
        var (generation, requestToken) = BeginRequest(ct);
        Scope = scope;
        ClearResults();
        IsInitialLoading = true;
        IsLoadingMore = false;
        IsForbidden = false;

        try
        {
            var response = await _client.GetAsync(CreateRequest(1), requestToken);
            if (!IsCurrent(generation))
                return;

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                SetForbidden();
                return;
            }

            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                Items = response.Content.Items;
                CurrentUser = response.Content.CurrentUser;
                Page = response.Content.Page;
                HasMore = response.Content.HasMore;
                return;
            }

            InitialErrorMessage = scope == RankingScope.Municipal && response.StatusCode == System.Net.HttpStatusCode.BadRequest
                ? MunicipalError
                : InitialError;
        }
        catch (OperationCanceledException) when (!IsCurrent(generation))
        {
        }
        catch (Exception) when (IsCurrent(generation))
        {
            InitialErrorMessage = InitialError;
        }
        finally
        {
            if (IsCurrent(generation))
                IsInitialLoading = false;
        }
    }

    private RequestGetRankingJson CreateRequest(int page) => new()
    {
        Scope = Scope,
        City = Scope == RankingScope.Municipal ? City : null,
        State = Scope == RankingScope.Municipal ? State : null,
        Page = page,
        PageSize = PageSize
    };

    private (long Generation, CancellationToken Token) BeginRequest(CancellationToken ct)
    {
        InvalidateRequest();
        _requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        return (_generation, _requestCancellation.Token);
    }

    private void InvalidateRequest()
    {
        _generation++;
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }

    private void CancelActiveRequest()
    {
        InvalidateRequest();
        IsInitialLoading = false;
        IsLoadingMore = false;
    }

    private bool IsCurrent(long generation) => generation == _generation;

    private void ClearResults()
    {
        Items = [];
        CurrentUser = null;
        Page = 0;
        HasMore = false;
        InitialErrorMessage = null;
        LoadMoreErrorMessage = null;
    }

    private void SetForbidden()
    {
        ClearResults();
        IsForbidden = true;
    }

    public void Dispose()
    {
        InvalidateRequest();
    }
}

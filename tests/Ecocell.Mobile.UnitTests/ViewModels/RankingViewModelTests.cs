using System.Net;
using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.ViewModels;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests.Ranking;
using Ecocell.Shared.Responses.Ranking;
using Moq;
using Refit;
using Shouldly;

namespace Ecocell.Mobile.UnitTests.ViewModels;

public sealed class RankingViewModelTests
{
    private readonly Mock<IRankingClient> _client = new();
    private readonly RankingViewModel _viewModel;

    public RankingViewModelTests() => _viewModel = new(_client.Object);

    [Fact]
    public async Task InitializeAsync_ShouldLoadFirstNationalPage_WhenResponseIsSuccessful()
    {
        // Arrange
        var ana = Item("Ana S.", 1, 10m, true);
        var response = Success(Page([ana], ana, 1, 20, true));

        // Act
        await LoadNationalFirstPageAsync(response);

        // Assert
        _viewModel.Scope.ShouldBe(RankingScope.National);
        _viewModel.Items.ShouldHaveSingleItem();
        _viewModel.Items[0].ShouldBe(ana);
        _viewModel.CurrentUser.ShouldBe(ana);
        _viewModel.Page.ShouldBe(1);
        _viewModel.HasMore.ShouldBeTrue();
        _viewModel.IsInitialLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowInitialError_WhenResponseIsNotSuccessful()
    {
        // Act
        await LoadNationalFirstPageAsync(Failure(HttpStatusCode.InternalServerError));

        // Assert
        _viewModel.InitialErrorMessage.ShouldBe("Não foi possível carregar o ranking. Tente novamente.");
        _viewModel.IsInitialLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowInitialError_WhenSuccessfulResponseHasNoContent()
    {
        // Arrange
        var response = new Mock<IApiResponse<ResponseRankingJson>>();
        response.SetupGet(value => value.IsSuccessStatusCode).Returns(true);
        response.SetupGet(value => value.Content).Returns((ResponseRankingJson?)null);

        // Act
        await LoadNationalFirstPageAsync(response.Object);

        // Assert
        _viewModel.InitialErrorMessage.ShouldBe("Não foi possível carregar o ranking. Tente novamente.");
        _viewModel.IsInitialLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowInitialError_WhenClientThrows()
    {
        // Arrange
        _client
            .Setup(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException());

        // Act
        await _viewModel.InitializeAsync(CancellationToken.None);

        // Assert
        _viewModel.InitialErrorMessage.ShouldBe("Não foi possível carregar o ranking. Tente novamente.");
        _viewModel.IsInitialLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task SearchMunicipalAsync_ShouldNotCallApi_WhenCityOrStateIsBlank()
    {
        await _viewModel.SelectScopeAsync(RankingScope.Municipal, CancellationToken.None);
        _viewModel.SetCity("Belo Horizonte");

        await _viewModel.SearchMunicipalAsync(CancellationToken.None);

        _client.Verify(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()), Times.Never);
        _viewModel.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchMunicipalAsync_ShouldLoadFirstPageWithRawFilters_WhenFiltersAreValid()
    {
        var maria = Item("Maria C.", 4, 8m, false);
        _client.Setup(client => client.GetAsync(
                It.Is<RequestGetRankingJson>(request => request.Scope == RankingScope.Municipal
                    && request.City == "  Belo Horizonte  " && request.State == "mg"
                    && request.Page == 1 && request.PageSize == RankingViewModel.PageSize),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(Page([maria], null, 1, 20, false)));

        await _viewModel.SelectScopeAsync(RankingScope.Municipal, CancellationToken.None);
        _viewModel.SetCity("  Belo Horizonte  ");
        _viewModel.SetState("mg");
        await _viewModel.SearchMunicipalAsync(CancellationToken.None);

        _viewModel.City.ShouldBe("  Belo Horizonte  ");
        _viewModel.State.ShouldBe("mg");
        _viewModel.Items.ShouldHaveSingleItem().ShouldBe(maria);
        _viewModel.Page.ShouldBe(1);
    }

    [Fact]
    public async Task LoadMoreAsync_ShouldAppendApiItemsWithoutChangingPositions_WhenMorePagesExist()
    {
        var first = Item("Ana S.", 1, 20m, false);
        var tied = Item("Bia R.", 1, 20m, false);
        var third = Item("Caio M.", 2, 10m, false);
        _client.SetupSequence(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(Page([first, tied], null, 1, 20, true)))
            .ReturnsAsync(Success(Page([third], null, 2, 20, false)));

        await _viewModel.InitializeAsync(CancellationToken.None);
        await _viewModel.LoadMoreAsync(CancellationToken.None);

        _viewModel.Items.Select(item => item.Position).ShouldBe([1, 1, 2]);
        _viewModel.Page.ShouldBe(2);
        _viewModel.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadMoreAsync_ShouldShowRecoverableMessageAndPreserveItems_WhenRequestFails()
    {
        var first = Item("Ana S.", 1, 20m, false);
        _client.SetupSequence(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(Page([first], null, 1, 20, true)))
            .ReturnsAsync(Failure(HttpStatusCode.InternalServerError));

        await _viewModel.InitializeAsync(CancellationToken.None);
        await _viewModel.LoadMoreAsync(CancellationToken.None);

        _viewModel.LoadMoreErrorMessage.ShouldBe("Não foi possível carregar mais posições. Tente novamente.");
        _viewModel.Items.ShouldHaveSingleItem().ShouldBe(first);
    }

    [Fact]
    public async Task RetryLoadMoreAsync_ShouldAppendNextPage_WhenPreviousIncrementalRequestFailed()
    {
        var first = Item("Ana S.", 1, 20m, false);
        var second = Item("Bia R.", 2, 10m, false);
        _client.SetupSequence(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(Page([first], null, 1, 20, true)))
            .ReturnsAsync(Failure(HttpStatusCode.InternalServerError))
            .ReturnsAsync(Success(Page([second], null, 2, 20, false)));

        await _viewModel.InitializeAsync(CancellationToken.None);
        await _viewModel.LoadMoreAsync(CancellationToken.None);
        await _viewModel.RetryLoadMoreAsync(CancellationToken.None);

        _viewModel.Items.ShouldBe([first, second]);
        _viewModel.LoadMoreErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task ShouldShowCurrentUserCard_ShouldBeTrue_WhenCurrentUserIsOutsideLoadedItems()
    {
        var inside = Item("Ana S.", 1, 20m, true);
        var outside = Item("Bia R.", 25, 5m, true);
        await LoadNationalFirstPageAsync(Success(Page([inside], outside, 1, 20, false)));

        _viewModel.ShouldShowCurrentUserCard.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldShowCurrentUserCard_ShouldBeFalse_WhenCurrentUserIsInLoadedItems()
    {
        var current = Item("Ana S.", 1, 20m, true);
        await LoadNationalFirstPageAsync(Success(Page([current], current, 1, 20, false)));

        _viewModel.ShouldShowCurrentUserCard.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldClearRankingAndSetForbidden_WhenApiReturnsForbidden()
    {
        var ana = Item("Ana S.", 1, 20m, true);
        await LoadNationalFirstPageAsync(Success(Page([ana], ana, 1, 20, true)));
        _client.Setup(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>())).ReturnsAsync(Failure(HttpStatusCode.Forbidden));

        await _viewModel.RetryInitialAsync(CancellationToken.None);

        _viewModel.Items.ShouldBeEmpty();
        _viewModel.CurrentUser.ShouldBeNull();
        _viewModel.InitialErrorMessage.ShouldBeNull();
        _viewModel.LoadMoreErrorMessage.ShouldBeNull();
        _viewModel.IsForbidden.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadMoreAsync_ShouldClearRankingAndSetForbidden_WhenApiReturnsForbidden()
    {
        var ana = Item("Ana S.", 1, 20m, true);
        _client.SetupSequence(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(Page([ana], ana, 1, 20, true)))
            .ReturnsAsync(Failure(HttpStatusCode.Forbidden));

        await _viewModel.InitializeAsync(CancellationToken.None);
        await _viewModel.LoadMoreAsync(CancellationToken.None);

        _viewModel.Items.ShouldBeEmpty();
        _viewModel.CurrentUser.ShouldBeNull();
        _viewModel.IsForbidden.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectScopeAsync_ShouldIgnoreStaleResponse_WhenNewerRequestCompletesFirst()
    {
        var nationalResponse = new TaskCompletionSource<IApiResponse<ResponseRankingJson>>();
        var municipalResponse = new TaskCompletionSource<IApiResponse<ResponseRankingJson>>();
        _client.SetupSequence(client => client.GetAsync(It.IsAny<RequestGetRankingJson>(), It.IsAny<CancellationToken>()))
            .Returns(nationalResponse.Task)
            .Returns(municipalResponse.Task);

        var national = _viewModel.InitializeAsync(CancellationToken.None);
        await _viewModel.SelectScopeAsync(RankingScope.Municipal, CancellationToken.None);
        _viewModel.SetCity("Betim");
        _viewModel.SetState("MG");
        var municipal = _viewModel.SearchMunicipalAsync(CancellationToken.None);
        var current = Item("Atual", 1, 15m, true);
        municipalResponse.SetResult(Success(Page([current], current, 1, 20, false)));
        await municipal;
        nationalResponse.SetResult(Success(Page([Item("Antigo", 1, 10m, true)], null, 1, 20, false)));
        await national;

        _viewModel.Items.ShouldHaveSingleItem().ShouldBe(current);
        _viewModel.Scope.ShouldBe(RankingScope.Municipal);
    }

    private static IApiResponse<ResponseRankingJson> Success(ResponseRankingJson content)
    {
        var response = new Mock<IApiResponse<ResponseRankingJson>>();
        response.SetupGet(value => value.IsSuccessStatusCode).Returns(true);
        response.SetupGet(value => value.Content).Returns(content);
        return response.Object;
    }

    private static IApiResponse<ResponseRankingJson> Failure(HttpStatusCode statusCode)
    {
        var response = new Mock<IApiResponse<ResponseRankingJson>>();
        response.SetupGet(value => value.IsSuccessStatusCode).Returns(false);
        response.SetupGet(value => value.StatusCode).Returns(statusCode);
        return response.Object;
    }

    private static ResponseRankingItemJson Item(string reducedName, long position, decimal points, bool isCurrentUser) => new()
    {
        ReducedName = reducedName,
        Position = position,
        Points = points,
        IsCurrentUser = isCurrentUser
    };

    private static ResponseRankingJson Page(
        IReadOnlyList<ResponseRankingItemJson> items,
        ResponseRankingItemJson? currentUser,
        int page,
        int pageSize,
        bool hasMore) => new()
    {
        Items = items,
        CurrentUser = currentUser,
        Page = page,
        PageSize = pageSize,
        HasMore = hasMore
    };

    private async Task LoadNationalFirstPageAsync(IApiResponse<ResponseRankingJson> response)
    {
        _client
            .Setup(client => client.GetAsync(
                It.Is<RequestGetRankingJson>(request =>
                    request.Scope == RankingScope.National
                    && request.Page == 1
                    && request.PageSize == RankingViewModel.PageSize
                    && request.City == null
                    && request.State == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await _viewModel.InitializeAsync(CancellationToken.None);
    }
}

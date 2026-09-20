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

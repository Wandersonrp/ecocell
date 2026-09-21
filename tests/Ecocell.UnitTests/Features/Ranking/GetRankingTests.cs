using Ecocell.Api.Features.Ranking;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.UnitTests.Features.Ranking;

public sealed class GetRankingTests : TestBase
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly GetRanking.Validator _validator = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly GetRanking.Handler _handler;

    public GetRankingTests()
    {
        _currentUserService
            .Setup(service => service.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveDepositor(_currentUserId));

        _handler = new GetRanking.Handler(DbContext, _validator, _currentUserService.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenScopeIsMissing()
    {
        var result = await _handler.Handle(new GetRanking.Query { Scope = null }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData(null, "MG")]
    [InlineData("", "MG")]
    [InlineData("   ", "MG")]
    [InlineData("Betim", null)]
    [InlineData("Betim", "")]
    [InlineData("Betim", "   ")]
    public async Task Handle_ShouldReturnValidationError_WhenMunicipalPairIsIncomplete(string? city, string? state)
    {
        var result = await _handler.Handle(new GetRanking.Query
        {
            Scope = SharedEnums.RankingScope.Municipal,
            City = city,
            State = state
        }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Handle_ShouldReturnValidationError_WhenPaginationIsInvalid(int page, int pageSize)
    {
        var result = await _handler.Handle(new GetRanking.Query
        {
            Scope = SharedEnums.RankingScope.National,
            Page = page,
            PageSize = pageSize
        }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenCurrentUserDoesNotExist()
    {
        _currentUserService
            .Setup(service => service.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        var result = await _handler.Handle(ValidNationalQuery(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Unauthorized);
    }

    [Theory]
    [InlineData(ApiEnums.Role.Admin, ApiEnums.PersonType.NaturalPerson, ApiEnums.Journey.Depositor, ApiEnums.PersonStatus.Active)]
    [InlineData(ApiEnums.Role.User, ApiEnums.PersonType.LegalPerson, ApiEnums.Journey.Depositor, ApiEnums.PersonStatus.Active)]
    [InlineData(ApiEnums.Role.User, ApiEnums.PersonType.NaturalPerson, ApiEnums.Journey.None, ApiEnums.PersonStatus.Active)]
    [InlineData(ApiEnums.Role.User, ApiEnums.PersonType.NaturalPerson, ApiEnums.Journey.Depositor, ApiEnums.PersonStatus.Suspended)]
    public async Task Handle_ShouldReturnForbidden_WhenCurrentUserProfileIsIneligible(
        ApiEnums.Role role,
        ApiEnums.PersonType personType,
        ApiEnums.Journey journey,
        ApiEnums.PersonStatus personStatus)
    {
        _currentUserService
            .Setup(service => service.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = _currentUserId,
                Role = role,
                PersonType = personType,
                Journey = journey,
                PersonStatus = personStatus
            });

        var result = await _handler.Handle(ValidNationalQuery(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldPreservePositionsAndReturnCurrentUserOutsidePage()
    {
        await CreateRankingViewAsync(
            new Seed("National", null, null, Guid.NewGuid(), "Ana Ativa", 30m, 1),
            new Seed("National", null, null, Guid.NewGuid(), "Bia Ativa", 30m, 1),
            new Seed("National", null, null, _currentUserId, "Caio da Silva", 10m, 2));

        var firstPage = await _handler.Handle(new GetRanking.Query
        {
            Scope = SharedEnums.RankingScope.National,
            Page = 1,
            PageSize = 2
        }, CancellationToken.None);

        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Select(item => item.Position).ShouldBe([1L, 1L]);
        firstPage.Value.HasMore.ShouldBeTrue();
        firstPage.Value.CurrentUser.ShouldNotBeNull();
        firstPage.Value.CurrentUser.Position.ShouldBe(2L);
        firstPage.Value.CurrentUser.ReducedName.ShouldBe("Caio S.");

        var secondPage = await _handler.Handle(new GetRanking.Query
        {
            Scope = SharedEnums.RankingScope.National,
            Page = 2,
            PageSize = 2
        }, CancellationToken.None);

        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.ShouldHaveSingleItem();
        secondPage.Value.Items[0].Position.ShouldBe(2L);
        secondPage.Value.Items[0].IsCurrentUser.ShouldBeTrue();
        secondPage.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNormalizeMunicipalPairAndIgnoreAnotherState()
    {
        await CreateRankingViewAsync(
            new Seed("Municipal", "SP", "são paulo", _currentUserId, "Ana Souza", 10m, 2),
            new Seed("Municipal", "SP", "são paulo", Guid.NewGuid(), "Bia Lima", 20m, 1),
            new Seed("Municipal", "RJ", "são paulo", Guid.NewGuid(), "Caio Lima", 100m, 1));

        var result = await _handler.Handle(new GetRanking.Query
        {
            Scope = SharedEnums.RankingScope.Municipal,
            City = " SÃO PAULO ",
            State = " sp ",
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.Items.ShouldNotContain(item => item.ReducedName == "Caio L.");
        result.Value.CurrentUser.ShouldNotBeNull();
        result.Value.CurrentUser.Position.ShouldBe(2L);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullCurrentUser_WhenDepositorDoesNotParticipateInScope()
    {
        await CreateRankingViewAsync(new Seed("Municipal", "MG", "betim", Guid.NewGuid(), "Bia Lima", 20m, 1));

        var result = await _handler.Handle(new GetRanking.Query
        {
            Scope = SharedEnums.RankingScope.Municipal,
            City = "Betim",
            State = "MG"
        }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CurrentUser.ShouldBeNull();
    }

    [Theory]
    [InlineData("Ana Souza", "Ana S.")]
    [InlineData("  Maria   da   Silva  ", "Maria S.")]
    [InlineData("João", "João")]
    [InlineData("   ", "")]
    public void ReduceName_ShouldReturnApprovedFormat(string fullName, string expected)
    {
        GetRanking.ReduceName(fullName).ShouldBe(expected);
    }

    private static GetRanking.Query ValidNationalQuery() => new()
    {
        Scope = SharedEnums.RankingScope.National,
        Page = 1,
        PageSize = 20
    };

    private static CurrentUserDto ActiveDepositor(Guid id) => new()
    {
        Id = id,
        Role = ApiEnums.Role.User,
        PersonType = ApiEnums.PersonType.NaturalPerson,
        Journey = ApiEnums.Journey.Depositor,
        PersonStatus = ApiEnums.PersonStatus.Active
    };

    private async Task CreateRankingViewAsync(params Seed[] rows)
    {
        await DbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE RankingSeed (
                Scope TEXT NOT NULL,
                State TEXT NULL,
                City TEXT NULL,
                DepositorId TEXT NOT NULL,
                FullName TEXT NOT NULL,
                TotalPoints NUMERIC NOT NULL,
                Position INTEGER NOT NULL
            );
            CREATE VIEW v_ranking_depositor AS
            SELECT Scope, State, City, DepositorId, FullName, TotalPoints, Position
            FROM RankingSeed;
            """);

        foreach (var row in rows)
        {
            await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO RankingSeed
                    (Scope, State, City, DepositorId, FullName, TotalPoints, Position)
                VALUES
                    ({row.Scope}, {row.State}, {row.City}, {row.DepositorId},
                     {row.FullName}, {row.TotalPoints}, {row.Position});
                """);
        }
    }

    private sealed record Seed(
        string Scope,
        string? State,
        string? City,
        Guid DepositorId,
        string FullName,
        decimal TotalPoints,
        long Position);
}

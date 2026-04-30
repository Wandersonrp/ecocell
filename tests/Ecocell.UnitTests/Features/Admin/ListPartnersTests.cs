using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Admin;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Admin;

public class ListPartnersTests : TestBase
{
    private readonly ListPartners.Handler _handler;
    private readonly ListPartners.Validator _validator;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;

    public ListPartnersTests()
    {
        _validator = new ListPartners.Validator();
        var loggerMock = CreateLoggerMock<ListPartners.Handler>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _handler = new ListPartners.Handler(DbContext, loggerMock.Object, _validator, _currentUserServiceMock.Object);

        _currentUserServiceMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = Guid.NewGuid(),
                Role = Role.Admin,
                PersonStatus = PersonStatus.Active,
                PersonType = PersonType.NaturalPerson,
                Journey = Journey.Depositor,
                Email = new Faker().Internet.Email()
            });
    }

    private LegalPerson CreatePartner(Journey journey = Journey.CollectPoint)
    {
        var partner = new LegalPerson(
            new Faker().Company.CompanyName(),
            new Faker().Company.CompanyName(),
            new Faker().Company.Cnpj(includeFormatSymbols: false),
            new Faker().Internet.Email(),
            journey);
        DbContext.LegalPeople.Add(partner);
        DbContext.SaveChanges();
        return partner;
    }

    [Fact]
    public async Task Handle_ShouldReturnAllPartners_WhenNoFiltersApplied()
    {
        CreatePartner();
        CreatePartner(Journey.Collector);

        var result = await _handler.Handle(new ListPartners.Command { PageSize = 20 }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_ShouldFilterByStatus_WhenStatusProvided()
    {
        var pending = CreatePartner();
        var active = CreatePartner();
        DbContext.People
            .Where(p => p.Id == active.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.Active));

        var command = new ListPartners.Command { Status = PersonStatus.PendingApproval, PageSize = 20 };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].ExternalId.ShouldBe(pending.Id);
    }

    [Fact]
    public async Task Handle_ShouldFilterByJourney_WhenJourneyProvided()
    {
        CreatePartner(Journey.CollectPoint);
        CreatePartner(Journey.Collector);

        var command = new ListPartners.Command { Journey = Journey.Collector, PageSize = 20 };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Journey.ShouldBe(Ecocell.Shared.Enums.Journey.Collector);
    }

    [Fact]
    public async Task Handle_ShouldReturnNextCursor_WhenMoreResultsExist()
    {
        for (var i = 0; i < 3; i++) CreatePartner();

        var result = await _handler.Handle(new ListPartners.Command { PageSize = 2 }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasMore.ShouldBeTrue();
        result.Value.NextCursor.ShouldNotBeNull();
        result.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullCursor_WhenOnLastPage()
    {
        CreatePartner();
        CreatePartner();

        var result = await _handler.Handle(new ListPartners.Command { PageSize = 10 }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasMore.ShouldBeFalse();
        result.Value.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnItemsAfterCursor_WhenCursorProvided()
    {
        CreatePartner();
        CreatePartner();
        CreatePartner();

        var firstPage = await _handler.Handle(new ListPartners.Command { PageSize = 1 }, CancellationToken.None);
        firstPage.IsSuccess.ShouldBeTrue();

        var cursor = firstPage.Value.NextCursor;
        var secondPage = await _handler.Handle(new ListPartners.Command { Cursor = cursor, PageSize = 10 }, CancellationToken.None);

        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsNotAdmin()
    {
        _currentUserServiceMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto { Role = Role.User, PersonStatus = PersonStatus.Active });

        var result = await _handler.Handle(new ListPartners.Command { PageSize = 20 }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }
}
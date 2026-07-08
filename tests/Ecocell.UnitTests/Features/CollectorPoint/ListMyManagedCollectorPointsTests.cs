using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.CollectorPoint;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class ListMyManagedCollectorPointsTests : TestBase
{
    private readonly ListMyManagedCollectorPoints.Handler _handler;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Guid _responsibleId;

    public ListMyManagedCollectorPointsTests()
    {
        var loggerMock = CreateLoggerMock<ListMyManagedCollectorPoints.Handler>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _handler = new ListMyManagedCollectorPoints.Handler(DbContext, loggerMock.Object, _currentUserMock.Object);
        _responsibleId = AddResponsiblePerson();

        _currentUserMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = _responsibleId,
                Role = Role.User,
                PersonStatus = PersonStatus.Active,
                PersonType = PersonType.NaturalPerson,
                Journey = Journey.Depositor,
                Email = new Faker().Internet.Email(),
            });
    }

    private Guid AddResponsiblePerson()
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);
        DbContext.NaturalPeople.Add(person);
        DbContext.SaveChanges();
        return person.Id;
    }

    private LegalPerson AddCollectPoint(Guid? responsibleId, Journey journey = Journey.CollectPoint)
    {
        var faker = new Faker("pt_BR");
        var pc = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            journey,
            responsiblePersonId: responsibleId);
        DbContext.LegalPeople.Add(pc);
        DbContext.SaveChanges();
        return pc;
    }

    [Fact]
    public async Task Handle_ShouldReturnMyCollectPoints_WhenResponsibleHasThem()
    {
        AddCollectPoint(_responsibleId);
        AddCollectPoint(_responsibleId);

        var result = await _handler.Handle(new ListMyManagedCollectorPoints.Query(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_ShouldExcludeOtherResponsiblePoints_WhenTheyBelongToAnotherPerson()
    {
        AddCollectPoint(_responsibleId);
        AddCollectPoint(AddResponsiblePerson());

        var result = await _handler.Handle(new ListMyManagedCollectorPoints.Query(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ShouldExcludeNonCollectPointJourneys_WhenResponsibleAlsoManagesCollector()
    {
        AddCollectPoint(_responsibleId, Journey.CollectPoint);
        AddCollectPoint(_responsibleId, Journey.Collector);

        var result = await _handler.Handle(new ListMyManagedCollectorPoints.Query(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenResponsibleManagesNone()
    {
        AddCollectPoint(AddResponsiblePerson());

        var result = await _handler.Handle(new ListMyManagedCollectorPoints.Query(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserIsSuspended()
    {
        AddCollectPoint(_responsibleId);
        _currentUserMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = _responsibleId,
                Role = Role.User,
                PersonStatus = PersonStatus.Suspended,
                PersonType = PersonType.NaturalPerson,
                Email = new Faker().Internet.Email(),
            });

        var result = await _handler.Handle(new ListMyManagedCollectorPoints.Query(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenPersonTypeIsNotNaturalPerson()
    {
        AddCollectPoint(_responsibleId);
        _currentUserMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = _responsibleId,
                Role = Role.User,
                PersonStatus = PersonStatus.Active,
                PersonType = PersonType.LegalPerson,
                Email = new Faker().Internet.Email(),
            });

        var result = await _handler.Handle(new ListMyManagedCollectorPoints.Query(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }
}

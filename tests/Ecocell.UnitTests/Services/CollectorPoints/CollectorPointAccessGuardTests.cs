using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Services.CollectorPoints;

public class CollectorPointAccessGuardTests : TestBase
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly CollectorPointAccessGuard _guard;
    private readonly Guid _responsibleId;

    public CollectorPointAccessGuardTests()
    {
        _responsibleId = AddNaturalPerson();
        SetCurrentUser(_responsibleId, Role.User, PersonStatus.Active, PersonType.NaturalPerson);
        _guard = new CollectorPointAccessGuard(
            DbContext,
            _currentUser.Object,
            CreateLoggerMock<CollectorPointAccessGuard>().Object);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldSucceed_WhenCallerOwnsActiveCollectorPoint()
    {
        var collectorPoint = AddLegalPerson(_responsibleId, Journey.CollectPoint, PersonStatus.Active);

        var result = await _guard.EnsureResponsibleActiveAsync(collectorPoint.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnForbidden_WhenCurrentUserIsMissing()
    {
        _currentUser.Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        var result = await _guard.EnsureResponsibleActiveAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Theory]
    [InlineData(Role.Admin, PersonStatus.Active, PersonType.NaturalPerson)]
    [InlineData(Role.Support, PersonStatus.Active, PersonType.NaturalPerson)]
    [InlineData(Role.User, PersonStatus.Suspended, PersonType.NaturalPerson)]
    [InlineData(Role.User, PersonStatus.Active, PersonType.LegalPerson)]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnForbidden_WhenCallerIsIneligible(
        Role role,
        PersonStatus status,
        PersonType personType)
    {
        SetCurrentUser(_responsibleId, role, status, personType);

        var result = await _guard.EnsureResponsibleActiveAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnNotFound_WhenCollectorPointDoesNotExist()
    {
        var result = await _guard.EnsureResponsibleActiveAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnNotFound_WhenLegalPersonHasAnotherJourney()
    {
        var collector = AddLegalPerson(_responsibleId, Journey.Collector, PersonStatus.Active);

        var result = await _guard.EnsureResponsibleActiveAsync(collector.Id, CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnNotFound_WhenAnotherPersonOwnsCollectorPoint()
    {
        var collectorPoint = AddLegalPerson(AddNaturalPerson(), Journey.CollectPoint, PersonStatus.Active);

        var result = await _guard.EnsureResponsibleActiveAsync(collectorPoint.Id, CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Theory]
    [InlineData(PersonStatus.PendingApproval)]
    [InlineData(PersonStatus.Suspended)]
    [InlineData(PersonStatus.Refused)]
    [InlineData(PersonStatus.Inactive)]
    [InlineData(PersonStatus.AwaitingConfirmation)]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnConflict_WhenOwnedCollectorPointIsNotActive(
        PersonStatus status)
    {
        var collectorPoint = AddLegalPerson(_responsibleId, Journey.CollectPoint, status);

        var result = await _guard.EnsureResponsibleActiveAsync(collectorPoint.Id, CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    private void SetCurrentUser(Guid id, Role role, PersonStatus status, PersonType personType)
    {
        _currentUser.Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = id,
                Role = role,
                PersonStatus = status,
                PersonType = personType,
                Journey = Journey.Depositor,
                Email = new Faker().Internet.Email(),
            });
    }

    private Guid AddNaturalPerson()
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);
        DbContext.NaturalPeople.Add(person);
        DbContext.SaveChanges();
        return person.Id;
    }

    private LegalPerson AddLegalPerson(Guid responsibleId, Journey journey, PersonStatus status)
    {
        var faker = new Faker("pt_BR");
        var legalPerson = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            journey,
            responsiblePersonId: responsibleId);
        DbContext.LegalPeople.Add(legalPerson);

        if (status == PersonStatus.Active)
            legalPerson.Approve(Role.Admin);
        else if (status == PersonStatus.Refused)
            legalPerson.Reject(Role.Admin);
        else if (status == PersonStatus.Suspended)
        {
            legalPerson.Approve(Role.Admin);
            legalPerson.Block(Role.Admin);
        }
        else if (status != PersonStatus.PendingApproval)
            DbContext.Entry(legalPerson).Property(x => x.PersonStatus).CurrentValue = status;

        DbContext.SaveChanges();
        return legalPerson;
    }
}

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

public class GeneratePcQrCodeTests : TestBase
{
    private readonly GeneratePcQrCode.Handler _handler;
    private readonly GeneratePcQrCode.Validator _validator;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Guid _responsibleId;

    public GeneratePcQrCodeTests()
    {
        var loggerMock = CreateLoggerMock<GeneratePcQrCode.Handler>();
        _validator = new GeneratePcQrCode.Validator();
        _currentUserMock = new Mock<ICurrentUserService>();
        _handler = new GeneratePcQrCode.Handler(DbContext, loggerMock.Object, _validator, _currentUserMock.Object);
        _responsibleId = AddResponsiblePerson();
        SetCurrentUser(_responsibleId, Role.User, PersonStatus.Active, PersonType.NaturalPerson);
    }

    private void SetCurrentUser(Guid id, Role role, PersonStatus status, PersonType type)
    {
        _currentUserMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = id,
                Role = role,
                PersonStatus = status,
                PersonType = type,
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

    // LegalPerson (CollectPoint/Collector) nasce PendingApproval. Ativa/transiciona via métodos internos.
    private LegalPerson AddCollectPoint(
        Guid? responsibleId,
        Journey journey = Journey.CollectPoint,
        PersonStatus status = PersonStatus.Active)
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

        switch (status)
        {
            case PersonStatus.Active:
                pc.Approve(Role.Admin);
                break;
            case PersonStatus.Refused:
                pc.Reject(Role.Admin);
                break;
            case PersonStatus.Suspended:
                pc.Approve(Role.Admin);
                pc.Block(Role.Admin);
                break;
            case PersonStatus.PendingApproval:
                break; // estado inicial da LegalPerson CollectPoint
            default:
                // Inactive/AwaitingConfirmation não têm transição de domínio para um PC;
                // força via EF (setter protegido) só para exercitar o branch genérico != Active.
                DbContext.Entry(pc).Property(p => p.PersonStatus).CurrentValue = status;
                break;
        }

        DbContext.SaveChanges();
        return pc;
    }

    [Fact]
    public async Task Handle_ShouldReturnQrString_WhenPcIsActive()
    {
        // Arrange
        var pc = AddCollectPoint(_responsibleId);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Qr.ShouldBe($"ecocell://pc/{pc.Id}");
    }

    [Theory]
    [InlineData(PersonStatus.PendingApproval)]
    [InlineData(PersonStatus.Suspended)]
    [InlineData(PersonStatus.Refused)]
    [InlineData(PersonStatus.Inactive)]
    [InlineData(PersonStatus.AwaitingConfirmation)]
    public async Task Handle_ShouldReturnConflict_WhenPcIsNotActive(PersonStatus status)
    {
        // Arrange
        var pc = AddCollectPoint(_responsibleId, status: status);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPcNotOwnedByCaller()
    {
        // Arrange
        var pc = AddCollectPoint(AddResponsiblePerson());

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPcDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLegalPersonIsNotCollectPoint()
    {
        // Arrange
        var collector = AddCollectPoint(_responsibleId, Journey.Collector);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(collector.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Support)]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsAdminOrSupport(Role role)
    {
        // Arrange
        var pc = AddCollectPoint(_responsibleId);
        SetCurrentUser(_responsibleId, role, PersonStatus.Active, PersonType.NaturalPerson);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsNotActiveNaturalPerson()
    {
        // Arrange
        var pc = AddCollectPoint(_responsibleId);
        SetCurrentUser(_responsibleId, Role.User, PersonStatus.Suspended, PersonType.NaturalPerson);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCurrentUserIsNull()
    {
        // Arrange
        var pc = AddCollectPoint(_responsibleId);
        _currentUserMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenPersonTypeIsNotNaturalPerson()
    {
        // Arrange
        var pc = AddCollectPoint(_responsibleId);
        SetCurrentUser(_responsibleId, Role.User, PersonStatus.Active, PersonType.LegalPerson);

        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(pc.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenIdIsEmpty()
    {
        // Act
        var result = await _handler.Handle(new GeneratePcQrCode.Query(Guid.Empty), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }
}

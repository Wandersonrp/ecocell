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

public class BlockPartnerTests : TestBase
{
    private readonly BlockPartner.Handler _handler;
    private readonly BlockPartner.Validator _validator;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly LegalPerson _partner;

    public BlockPartnerTests()
    {
        _validator = new BlockPartner.Validator();
        var loggerMock = CreateLoggerMock<BlockPartner.Handler>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _handler = new BlockPartner.Handler(DbContext, loggerMock.Object, _validator, _currentUserServiceMock.Object);

        _partner = new LegalPerson(
            new Faker().Company.CompanyName(),
            new Faker().Company.CompanyName(),
            new Faker().Company.Cnpj(includeFormatSymbols: false),
            new Faker().Internet.Email(),
            Journey.CollectPoint);
        DbContext.LegalPeople.Add(_partner);
        DbContext.SaveChanges();

        // Arrange: parceiro ativo (estado necessário para bloquear)
        DbContext.People
            .Where(p => p.Id == _partner.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.Active));
        DbContext.ChangeTracker.Clear();

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

    [Fact]
    public async Task Handle_ShouldBlockPartner_WhenCallerIsAdminAndPartnerIsActive()
    {
        var result = await _handler.Handle(
            new BlockPartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var updated = await DbContext.LegalPeople.FirstAsync(lp => lp.Id == _partner.Id);
        updated.PersonStatus.ShouldBe(PersonStatus.Suspended);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPartnerDoesNotExist()
    {
        var result = await _handler.Handle(
            new BlockPartner.Command { PartnerId = Guid.NewGuid() }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsNotAdmin()
    {
        _currentUserServiceMock
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto { Role = Role.User, PersonStatus = PersonStatus.Active });

        var result = await _handler.Handle(
            new BlockPartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenPartnerIsNotActive()
    {
        DbContext.People
            .Where(p => p.Id == _partner.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.PendingApproval));
        DbContext.ChangeTracker.Clear();

        var result = await _handler.Handle(
            new BlockPartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }
}
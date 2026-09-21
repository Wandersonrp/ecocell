using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Admin;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Admin;

public class ApprovePartnerTests : TestBase
{
    private readonly ApprovePartner.Handler _handler;
    private readonly ApprovePartner.Validator _validator;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly LegalPerson _partner;

    public ApprovePartnerTests()
    {
        _validator = new ApprovePartner.Validator();
        var loggerMock = CreateLoggerMock<ApprovePartner.Handler>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _emailSenderMock = new Mock<IEmailSender>();
        _handler = new ApprovePartner.Handler(
            DbContext, loggerMock.Object, _validator,
            _currentUserServiceMock.Object, _emailSenderMock.Object);

        _partner = new LegalPerson(
            new Faker().Company.CompanyName(),
            new Faker().Company.CompanyName(),
            new Faker().Company.Cnpj(includeFormatSymbols: false),
            new Faker().Internet.Email(),
            Journey.CollectPoint);
        DbContext.LegalPeople.Add(_partner);
        DbContext.SaveChanges();

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
    public async Task Handle_ShouldApprovePartner_WhenCallerIsAdminAndPartnerIsPending()
    {
        var result = await _handler.Handle(
            new ApprovePartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var updated = await DbContext.LegalPeople.FirstAsync(lp => lp.Id == _partner.Id);
        updated.PersonStatus.ShouldBe(PersonStatus.Active);
    }

    [Fact]
    public async Task Handle_ShouldSendApprovalEmail_WhenPartnerIsApproved()
    {
        await _handler.Handle(
            new ApprovePartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        _emailSenderMock.Verify(
            s => s.SendAsync(
                _partner.Email,
                EmailType.PartnerApproval,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPartnerDoesNotExist()
    {
        var result = await _handler.Handle(
            new ApprovePartner.Command { PartnerId = Guid.NewGuid() }, CancellationToken.None);

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
            new ApprovePartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenPartnerIsNotPending()
    {
        DbContext.People
            .Where(p => p.Id == _partner.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.Active));
        DbContext.ChangeTracker.Clear();

        var result = await _handler.Handle(
            new ApprovePartner.Command { PartnerId = _partner.Id }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }
}

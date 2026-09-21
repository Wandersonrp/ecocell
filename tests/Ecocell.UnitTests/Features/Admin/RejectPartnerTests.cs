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

public class RejectPartnerTests : TestBase
{
    private readonly RejectPartner.Handler _handler;
    private readonly RejectPartner.Validator _validator;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly LegalPerson _partner;

    public RejectPartnerTests()
    {
        _validator = new RejectPartner.Validator();
        var loggerMock = CreateLoggerMock<RejectPartner.Handler>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _emailSenderMock = new Mock<IEmailSender>();
        _handler = new RejectPartner.Handler(
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
    public async Task Handle_ShouldRejectPartner_WhenCallerIsAdminAndPartnerIsPending()
    {
        var command = new RejectPartner.Command
        {
            PartnerId = _partner.Id,
            Reason = "Documentação incompleta."
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var updated = await DbContext.LegalPeople.FirstAsync(lp => lp.Id == _partner.Id);
        updated.PersonStatus.ShouldBe(PersonStatus.Refused);
    }

    [Fact]
    public async Task Handle_ShouldSendRejectionEmail_WhenPartnerIsRejected()
    {
        var command = new RejectPartner.Command
        {
            PartnerId = _partner.Id,
            Reason = "Documentação incompleta."
        };

        await _handler.Handle(command, CancellationToken.None);

        _emailSenderMock.Verify(
            s => s.SendAsync(
                _partner.Email,
                EmailType.PartnerRejection,
                It.Is<IReadOnlyDictionary<string, string>>(d => d["reason"] == command.Reason),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPartnerDoesNotExist()
    {
        var result = await _handler.Handle(
            new RejectPartner.Command { PartnerId = Guid.NewGuid(), Reason = "Motivo." },
            CancellationToken.None);

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
            new RejectPartner.Command { PartnerId = _partner.Id, Reason = "Motivo." },
            CancellationToken.None);

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
            new RejectPartner.Command { PartnerId = _partner.Id, Reason = "Motivo." },
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }
}
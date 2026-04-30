using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class PersonTests
{
    private LegalPerson CreatePendingPartner() =>
        new LegalPerson(
            new Faker().Company.CompanyName(),
            new Faker().Company.CompanyName(),
            new Faker().Company.Cnpj(includeFormatSymbols: false),
            new Faker().Internet.Email(),
            Journey.CollectPoint);

    private NaturalPerson CreateNaturalPerson() =>
        new NaturalPerson(
            new Faker().Name.FullName(),
            new Faker().Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            Role.User,
            new Faker().Internet.Email(),
            Journey.Depositor);

    [Fact]
    public void Approve_ShouldTransitionToActive_WhenCallerIsAdminAndStatusIsPendingApproval()
    {
        var partner = CreatePendingPartner();
        partner.Approve(Role.Admin);
        partner.PersonStatus.ShouldBe(PersonStatus.Active);
    }

    [Fact]
    public void Approve_ShouldThrow_WhenCallerIsNotAdmin()
    {
        var partner = CreatePendingPartner();
        Should.Throw<UnauthorizedAccessException>(() => partner.Approve(Role.User));
    }

    [Fact]
    public void Approve_ShouldThrow_WhenStatusIsNotPendingApproval()
    {
        var person = CreateNaturalPerson(); // status = AwaitingConfirmation
        Should.Throw<InvalidOperationException>(() => person.Approve(Role.Admin));
    }

    [Fact]
    public void Reject_ShouldTransitionToRefused_WhenCallerIsAdminAndStatusIsPendingApproval()
    {
        var partner = CreatePendingPartner();
        partner.Reject(Role.Admin);
        partner.PersonStatus.ShouldBe(PersonStatus.Refused);
    }

    [Fact]
    public void Reject_ShouldThrow_WhenCallerIsNotAdmin()
    {
        var partner = CreatePendingPartner();
        Should.Throw<UnauthorizedAccessException>(() => partner.Reject(Role.User));
    }

    [Fact]
    public void Block_ShouldTransitionToSuspended_WhenCallerIsAdminAndStatusIsActive()
    {
        var partner = CreatePendingPartner();
        partner.Approve(Role.Admin); // Arrange: Active
        partner.Block(Role.Admin);
        partner.PersonStatus.ShouldBe(PersonStatus.Suspended);
    }

    [Fact]
    public void Block_ShouldThrow_WhenCallerIsNotAdmin()
    {
        var partner = CreatePendingPartner();
        partner.Approve(Role.Admin);
        Should.Throw<UnauthorizedAccessException>(() => partner.Block(Role.User));
    }

    [Fact]
    public void Block_ShouldThrow_WhenStatusIsNotActive()
    {
        var partner = CreatePendingPartner(); // status = PendingApproval
        Should.Throw<InvalidOperationException>(() => partner.Block(Role.Admin));
    }
}
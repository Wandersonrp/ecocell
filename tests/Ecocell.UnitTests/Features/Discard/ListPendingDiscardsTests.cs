using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Discard;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;
using DiscardEntity = Ecocell.Api.Entities.Discard;

namespace Ecocell.UnitTests.Features.Discard;

public sealed class ListPendingDiscardsTests : TestBase
{
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();

    [Fact]
    public async Task Validate_ShouldFail_WhenCollectorPointIdIsEmpty()
    {
        var result = await new ListPendingDiscards.Validator()
            .ValidateAsync(new ListPendingDiscards.Query(Guid.Empty));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenCollectorPointIdIsEmpty()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new ListPendingDiscards.Query(Guid.Empty), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        _guard.Verify(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPropagateForbidden_WhenCallerIsIneligible()
    {
        var point = AddPoint();
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(point.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Forbidden()));

        var result = await CreateHandler().Handle(
            new ListPendingDiscards.Query(point.Id), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoDiscardIsPending()
    {
        var point = AddPoint();
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(point.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await CreateHandler().Handle(
            new ListPendingDiscards.Query(point.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyPendingDiscardsFromRequestedPoint()
    {
        var point = AddPoint();
        var otherPoint = AddPoint();
        var depositor = AddDepositor();
        AddPending(point, depositor);
        AddPending(otherPoint, depositor);
        var terminal = AddPending(point, depositor);
        terminal.Reject();
        await DbContext.SaveChangesAsync();
        _guard.Setup(value => value.EnsureResponsibleActiveAsync(point.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await CreateHandler().Handle(
            new ListPendingDiscards.Query(point.Id), CancellationToken.None);

        result.Value.Items.ShouldHaveSingleItem();
        result.Value.Items.Single().DepositorName.ShouldBe(depositor.FullName);
    }

    private ListPendingDiscards.Handler CreateHandler() => new(
        DbContext,
        CreateLoggerMock<ListPendingDiscards.Handler>().Object,
        new ListPendingDiscards.Validator(),
        _guard.Object);

    private NaturalPerson AddDepositor()
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(
            faker.Name.FullName(), faker.Person.Cpf(false), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User, faker.Internet.Email(), Journey.Depositor);
        DbContext.NaturalPeople.Add(person);
        DbContext.SaveChanges();
        return person;
    }

    private LegalPerson AddPoint()
    {
        var faker = new Faker("pt_BR");
        var responsible = new NaturalPerson(
            faker.Name.FullName(), faker.Person.Cpf(false), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            Role.User, faker.Internet.Email(), Journey.Collector);
        var point = new LegalPerson(
            faker.Company.CompanyName(), faker.Company.CompanyName(), faker.Company.Cnpj(false),
            faker.Internet.Email(), Journey.CollectPoint, responsiblePersonId: responsible.Id);
        point.Approve(Role.Admin);
        DbContext.NaturalPeople.Add(responsible);
        DbContext.LegalPeople.Add(point);
        DbContext.SaveChanges();
        return point;
    }

    private DiscardEntity AddPending(LegalPerson point, NaturalPerson depositor)
    {
        var material = Enum.GetValues<ElectronicMaterial>()
            .Where(value => Convert.ToInt32(value) > 0)
            .First(value => !DbContext.MaterialScoreRules.Any(rule =>
                rule.LegalPersonId == point.Id && rule.Material == value));
        var rule = new MaterialScoreRule(point.Id, material, 1, MaterialScoreUnit.PerUnit, DateTime.UtcNow.AddDays(-1));
        var discard = new DiscardEntity(depositor.Id, point.Id, [new DiscardItem(material, 1, .1m, rule.Id)]);
        DbContext.MaterialScoreRules.Add(rule);
        DbContext.Discards.Add(discard);
        DbContext.SaveChanges();
        return discard;
    }
}

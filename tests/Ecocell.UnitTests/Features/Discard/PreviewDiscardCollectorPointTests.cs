using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Discard;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Discard;

public class PreviewDiscardCollectorPointTests : TestBase
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly PreviewDiscardCollectorPoint.Validator _validator = new();
    private readonly Guid _depositorId;

    public PreviewDiscardCollectorPointTests()
    {
        _depositorId = AddDepositor();
        SetCurrentUser(PersonStatus.Active, PersonType.NaturalPerson, Journey.Depositor);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenQrCodeIsInvalid()
    {
        var result = await _validator.ValidateAsync(
            new PreviewDiscardCollectorPoint.Query("invalid"), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PersonStatus.Suspended, PersonType.NaturalPerson, Journey.Depositor)]
    [InlineData(PersonStatus.Active, PersonType.LegalPerson, Journey.Depositor)]
    [InlineData(PersonStatus.Active, PersonType.NaturalPerson, Journey.None)]
    public async Task Handle_ShouldReturnForbidden_WhenDepositorIsIneligible(
        PersonStatus status,
        PersonType type,
        Journey journey)
    {
        SetCurrentUser(status, type, journey);

        var result = await CreateHandler().Handle(ValidQuery(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCollectorPointDoesNotExist()
    {
        var result = await CreateHandler().Handle(ValidQuery(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCollectorPointIsInactive()
    {
        var point = AddCollectorPoint(PersonStatus.PendingApproval);

        var result = await CreateHandler().Handle(ValidQuery(point.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyMaterials_WhenNoRuleIsActive()
    {
        var point = AddCollectorPoint();

        var result = await CreateHandler().Handle(ValidQuery(point.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AcceptedMaterials.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyActiveMaterialsInEnumOrder()
    {
        var address = new Address("Rua das Flores", "123", "Centro", "Belo Horizonte", "MG", "30123456");
        DbContext.Addresses.Add(address);
        DbContext.SaveChanges();
        var point = AddCollectorPoint(addressId: address.Id);
        AddRule(point.Id, ElectronicMaterial.Notebook, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        AddRule(point.Id, ElectronicMaterial.CellPhone, DateTime.UtcNow.AddDays(1));
        AddRule(point.Id, ElectronicMaterial.Notebook, DateTime.UtcNow.AddDays(-1));
        AddRule(point.Id, ElectronicMaterial.Battery, DateTime.UtcNow.AddDays(-1));

        var result = await CreateHandler().Handle(
            new PreviewDiscardCollectorPoint.Query($"ecocell://pc/{point.Id:D}"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TradeName.ShouldBe(point.TradeName);
        result.Value.AcceptedMaterials.ShouldBe([
            Ecocell.Shared.Enums.ElectronicMaterial.Battery,
            Ecocell.Shared.Enums.ElectronicMaterial.Notebook,
        ]);
        result.Value.FormattedAddress.ShouldContain(address.Street);
    }

    [Fact]
    public async Task Handle_ShouldFormatAddressWithoutPersonalData()
    {
        var address = new Address("Rua das Flores", "123", "Centro", "Belo Horizonte", "MG", "30123456", "Sala 4");
        DbContext.Addresses.Add(address);
        await DbContext.SaveChangesAsync();
        var point = AddCollectorPoint(addressId: address.Id);

        var result = await CreateHandler().Handle(
            new PreviewDiscardCollectorPoint.Query($"ecocell://pc/{point.Id:D}"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TradeName.ShouldBe(point.TradeName);
        result.Value.FormattedAddress.ShouldContain(address.Street);
        result.Value.FormattedAddress.ShouldContain(address.Complement!);
        typeof(Ecocell.Shared.Responses.Discards.ResponseDiscardPreviewJson).GetProperty("Cnpj").ShouldBeNull();
        typeof(Ecocell.Shared.Responses.Discards.ResponseDiscardPreviewJson).GetProperty("Email").ShouldBeNull();
    }

    private PreviewDiscardCollectorPoint.Handler CreateHandler() =>
        new(DbContext, CreateLoggerMock<PreviewDiscardCollectorPoint.Handler>().Object, _validator, _currentUserMock.Object);

    private Guid AddDepositor()
    {
        var faker = new Bogus.Faker("pt_BR");
        var person = new NaturalPerson(
            faker.Name.FullName(), faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)), Role.User, faker.Internet.Email(), Journey.Depositor);
        DbContext.NaturalPeople.Add(person);
        DbContext.SaveChanges();
        return person.Id;
    }

    private LegalPerson AddCollectorPoint(PersonStatus status = PersonStatus.Active, Guid? addressId = null)
    {
        var faker = new Bogus.Faker("pt_BR");
        var point = new LegalPerson(
            faker.Company.CompanyName(), faker.Company.CompanyName(), faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(), Journey.CollectPoint, addressId, _depositorId);
        DbContext.LegalPeople.Add(point);
        if (status == PersonStatus.Active)
            point.Approve(Role.Admin);
        DbContext.SaveChanges();
        return point;
    }

    private void AddRule(Guid pointId, ElectronicMaterial material, DateTime validFrom, DateTime? validTo = null)
    {
        var rule = new MaterialScoreRule(pointId, material, 10m, MaterialScoreUnit.PerUnit, validFrom);
        if (validTo is not null)
            rule.Close(validTo.Value);
        DbContext.MaterialScoreRules.Add(rule);
        DbContext.SaveChanges();
    }

    private void SetCurrentUser(PersonStatus status, PersonType type, Journey journey) =>
        _currentUserMock.Setup(value => value.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto { Id = _depositorId, Role = Role.User, PersonStatus = status, PersonType = type, Journey = journey });

    private static PreviewDiscardCollectorPoint.Query ValidQuery(Guid? pointId = null) =>
        new($"ecocell://pc/{pointId ?? Guid.NewGuid():D}");
}

using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Discard;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Discard;

public class RegisterDiscardTests : TestBase
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly RegisterDiscard.Validator _validator = new();
    private readonly Guid _depositorId;

    public RegisterDiscardTests()
    {
        _depositorId = AddDepositor();
        SetCurrentUser(
            _depositorId,
            Role.User,
            PersonStatus.Active,
            PersonType.NaturalPerson,
            Journey.Depositor);
    }

    private static ElectronicMaterial Material =>
        Enum.GetValues<ElectronicMaterial>().First();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand(), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-qr")]
    [InlineData("https://pc/018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
    [InlineData("ecocell://collector/018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
    [InlineData("ecocell://pc/not-a-guid")]
    public async Task Validate_ShouldFail_WhenQrCodeIsInvalid(string qrCode)
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { QrCode = qrCode }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreEmpty()
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreNull()
    {
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = null! }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
    {
        var item = ValidCommand().Items[0];
        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(ValidCommand() with { Items = [item, item] }, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
    {
        var command = ValidCommand();
        command.Items[0].Quantity = quantity;

        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(command, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    public async Task Validate_ShouldFail_WhenWeightIsNotPositive(string weight)
    {
        var command = ValidCommand();
        command.Items[0].ApproximateWeightKg = decimal.Parse(
            weight,
            System.Globalization.CultureInfo.InvariantCulture);

        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(command, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
    {
        var command = ValidCommand();
        var invalidItem = command.Items[0] with { Material = (ElectronicMaterial)0 };
        var invalidCommand = command with { Items = [invalidItem] };

        var result = await new RegisterDiscard.Validator()
            .ValidateAsync(invalidCommand, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPersistPendingDiscard_WhenRequestIsValid()
    {
        var point = AddCollectorPoint();
        var rule = AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(Ecocell.Shared.Enums.DiscardStatus.Pending);

        var discard = await DbContext.Discards
            .Include(value => value.Items)
            .SingleAsync();

        discard.DepositorId.ShouldBe(_depositorId);
        discard.CollectorPointId.ShouldBe(point.Id);
        discard.Status.ShouldBe(DiscardStatus.Pending);
        discard.Items.Single().MaterialScoreRuleId.ShouldBe(rule.Id);
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
        var point = AddCollectorPoint();
        AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));
        SetCurrentUser(_depositorId, Role.User, status, type, journey);

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
        (await DbContext.Discards.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Support)]
    public async Task Handle_ShouldPersistDiscard_WhenPrivilegedUserIsDepositor(Role role)
    {
        var point = AddCollectorPoint();
        AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));
        SetCurrentUser(
            _depositorId,
            role,
            PersonStatus.Active,
            PersonType.NaturalPerson,
            Journey.Depositor);

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await DbContext.Discards.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCurrentUserIsNull()
    {
        var point = AddCollectorPoint();
        AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));
        _currentUserMock
            .Setup(value => value.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
        (await DbContext.Discards.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCollectorPointDoesNotExist()
    {
        var result = await CreateHandler()
            .Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLegalPersonIsNotCollectorPoint()
    {
        var collector = AddCollectorPoint(journey: Journey.Collector);

        var result = await CreateHandler()
            .Handle(ValidCommand(collector.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCollectorPointIsInactive()
    {
        var point = AddCollectorPoint(PersonStatus.PendingApproval);

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenMaterialRuleIsExpired()
    {
        var point = AddCollectorPoint();
        AddRule(
            point.Id,
            Material,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(-1));

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        (await DbContext.Discards.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenMaterialRuleIsFuture()
    {
        var point = AddCollectorPoint();
        AddRule(point.Id, Material, DateTime.UtcNow.AddDays(1));

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        (await DbContext.Discards.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldNotPersistPartially_WhenOneMaterialIsUnsupported()
    {
        var point = AddCollectorPoint();
        var materials = Enum.GetValues<ElectronicMaterial>()
            .Where(value => Convert.ToInt32(value) > 0)
            .Take(2)
            .ToArray();
        materials.Length.ShouldBe(2);
        AddRule(point.Id, materials[0], DateTime.UtcNow.AddDays(-1));

        var command = ValidCommand(point.Id) with
        {
            Items =
            [
                new RegisterDiscard.ItemCommand
                {
                    Material = materials[0],
                    Quantity = 1,
                    ApproximateWeightKg = 0.200m,
                },
                new RegisterDiscard.ItemCommand
                {
                    Material = materials[1],
                    Quantity = 1,
                    ApproximateWeightKg = 0.300m,
                },
            ],
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
        (await DbContext.Discards.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ShouldKeepOriginalRuleId_WhenRuleIsVersionedLater()
    {
        var point = AddCollectorPoint();
        var original = AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));

        var result = await CreateHandler()
            .Handle(ValidCommand(point.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        DbContext.Entry(original).Property(value => value.ValidTo).CurrentValue = DateTime.UtcNow;
        AddRule(point.Id, Material, DateTime.UtcNow);
        await DbContext.SaveChangesAsync();

        var item = await DbContext.DiscardItems.SingleAsync();
        item.MaterialScoreRuleId.ShouldBe(original.Id);
    }

    private RegisterDiscard.Handler CreateHandler() =>
        new(
            DbContext,
            CreateLoggerMock<RegisterDiscard.Handler>().Object,
            _validator,
            _currentUserMock.Object);

    private Guid AddDepositor(
        Role role = Role.User,
        Journey journey = Journey.Depositor)
    {
        var faker = new Bogus.Faker("pt_BR");
        var person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            role,
            faker.Internet.Email(),
            journey);

        DbContext.NaturalPeople.Add(person);
        DbContext.SaveChanges();
        return person.Id;
    }

    private void SetCurrentUser(
        Guid id,
        Role role,
        PersonStatus status,
        PersonType type,
        Journey journey)
    {
        _currentUserMock
            .Setup(value => value.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = id,
                Role = role,
                PersonStatus = status,
                PersonType = type,
                Journey = journey,
            });
    }

    private LegalPerson AddCollectorPoint(
        PersonStatus status = PersonStatus.Active,
        Journey journey = Journey.CollectPoint)
    {
        var faker = new Bogus.Faker("pt_BR");
        var point = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            journey,
            responsiblePersonId: _depositorId);

        DbContext.LegalPeople.Add(point);

        if (status == PersonStatus.Active)
            point.Approve(Role.Admin);

        DbContext.SaveChanges();
        return point;
    }

    private MaterialScoreRule AddRule(
        Guid collectorPointId,
        ElectronicMaterial material,
        DateTime validFrom,
        DateTime? validTo = null)
    {
        var rule = new MaterialScoreRule(
            collectorPointId,
            material,
            10m,
            MaterialScoreUnit.PerUnit,
            validFrom);

        if (validTo is not null)
            rule.Close(validTo.Value);

        DbContext.MaterialScoreRules.Add(rule);
        DbContext.SaveChanges();
        return rule;
    }

    private static RegisterDiscard.Command ValidCommand(Guid? pointId = null) =>
        new()
        {
            QrCode = $"ecocell://pc/{pointId ?? Guid.NewGuid():D}",
            Items =
            [
                new RegisterDiscard.ItemCommand
                {
                    Material = Material,
                    Quantity = 1,
                    ApproximateWeightKg = 0.250m,
                },
            ],
        };
}

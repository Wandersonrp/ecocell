using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Shared;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

/// <summary>
/// Testes de unidade para o slice GetNaturalPersonProfile.
/// </summary>
public class GetNaturalPersonProfileTests : TestBase
{
    private readonly GetNaturalPersonProfile.Handler _handler;
    private readonly GetNaturalPersonProfile.Validator _validator;
    private readonly NaturalPerson _person;

    public GetNaturalPersonProfileTests()
    {
        _validator = new GetNaturalPersonProfile.Validator();
        var loggerMock = CreateLoggerMock<GetNaturalPersonProfile.Handler>();

        _handler = new GetNaturalPersonProfile.Handler(DbContext, loggerMock.Object, _validator);

        var faker = new Faker("pt_BR");
        _person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);

        DbContext.People.Add(_person);
        DbContext.SaveChanges();
    }

    [Fact]
    public async Task Handle_ShouldReturnProfile_WhenPersonExists()
    {
        // Act
        var result = await _handler.Handle(new GetNaturalPersonProfile.Query(_person.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(_person.Id);
        result.Value.FullName.ShouldBe(_person.FullName);
        result.Value.Email.ShouldBe(_person.Email);
        result.Value.Cpf.ShouldBe(_person.Cpf);
        result.Value.BirthDate.ShouldBe(_person.BirthDate);
        result.Value.Role.ShouldBe(Ecocell.Shared.Enums.Role.User);
        result.Value.Journey.ShouldBe(Ecocell.Shared.Enums.Journey.Depositor);
        result.Value.PersonType.ShouldBe(Ecocell.Shared.Enums.PersonType.NaturalPerson);
        result.Value.PersonStatus.ShouldBe(Ecocell.Shared.Enums.PersonStatus.AwaitingConfirmation);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPersonDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(new GetNaturalPersonProfile.Query(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
        result.Error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenPersonIdIsEmpty()
    {
        // Act
        var result = await _handler.Handle(new GetNaturalPersonProfile.Query(Guid.Empty), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        result.Error.Messages.ShouldHaveSingleItem();
    }
}

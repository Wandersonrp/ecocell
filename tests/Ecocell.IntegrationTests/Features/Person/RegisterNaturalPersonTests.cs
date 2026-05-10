using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Person;

public class RegisterNaturalPersonTests : IntegrationTestBase
{
    private readonly RequestRegisterNaturalPerson _request;

    public RegisterNaturalPersonTests(IntegrationTestFixture fixture) : base(fixture)
    {
        var faker = new Faker("pt_BR");
        _request = new RequestRegisterNaturalPerson
        {
            Email = faker.Internet.Email(),
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            FullName = faker.Name.FullName(),
            Journey = Journey.Depositor,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        };
    }

    [Fact]
    public async Task Post_ShouldReturn201_WhenRequestIsValid()
    {
        // Act
        var response = await Client.PostAsJsonAsync("api/natural-person", _request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenCpfAlreadyExists()
    {
        // Arrange — cadastra a primeira vez
        var first = await Client.PostAsJsonAsync("api/natural-person", _request);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Arrange — segundo request com mesmo CPF, e-mail diferente
        var duplicate = new RequestRegisterNaturalPerson
        {
            Email = new Faker().Internet.Email(),
            Cpf = _request.Cpf,
            FullName = _request.FullName,
            Journey = _request.Journey,
            BirthDate = _request.BirthDate,
        };

        // Act
        var response = await Client.PostAsJsonAsync("api/natural-person", duplicate);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}

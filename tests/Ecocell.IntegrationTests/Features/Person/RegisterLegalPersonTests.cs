using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Person;

public class RegisterLegalPersonTests : IntegrationTestBase
{
    public RegisterLegalPersonTests(IntegrationTestFixture fixture) : base(fixture) { }

    private static RequestRegisterLegalPerson BuildValidRequest()
    {
        var faker = new Faker("pt_BR");
        return new RequestRegisterLegalPerson
        {
            Cnpj = faker.Company.Cnpj(includeFormatSymbols: false),
            LegalName = faker.Company.CompanyName(),
            TradeName = faker.Company.CompanyName(),
            Email = faker.Internet.Email(),
            Journey = Journey.CollectPoint,
            Address = new RequestRegisterLegalPersonAddress
            {
                Street = faker.Address.StreetName(),
                Number = faker.Random.Number(1, 9999).ToString(),
                Complement = null,
                Neighborhood = faker.Address.City(),
                City = faker.Address.City(),
                State = "SP",
                ZipCode = "01310100",
            },
        };
    }

    [Fact]
    public async Task Post_ShouldReturn201_WhenJwtIsValidAndRequestIsValid()
    {
        // Arrange — authenticated natural person as responsible
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsJsonAsync("api/legal-person", BuildValidRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenNoToken()
    {
        // Act — no Authorization header
        var response = await Client.PostAsJsonAsync("api/legal-person", BuildValidRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

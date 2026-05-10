using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Account;

public class ConfirmAccountTests : IntegrationTestBase
{
    public ConfirmAccountTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn200_WhenCodeIsValid()
    {
        // Arrange — register a person to get the confirmation code
        var faker = new Faker("pt_BR");
        var email = faker.Internet.Email();

        var registerRequest = new RequestRegisterNaturalPerson
        {
            Email = email,
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            FullName = faker.Name.FullName(),
            Journey = Journey.Depositor,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        };
        await Client.PostAsJsonAsync("api/natural-person", registerRequest);

        var code = GetCapturingEmailSender().GetCapturedCode(email);
        code.ShouldNotBeNull();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ShouldReturnError_WhenCodeIsWrong()
    {
        // Arrange — register a person
        var faker = new Faker("pt_BR");
        var email = faker.Internet.Email();

        var registerRequest = new RequestRegisterNaturalPerson
        {
            Email = email,
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            FullName = faker.Name.FullName(),
            Journey = Journey.Depositor,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        };
        await Client.PostAsJsonAsync("api/natural-person", registerRequest);

        // Act — wrong code
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = "000000" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }
}

using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Account;

public class ResendVerificationCodeTests : IntegrationTestBase
{
    public ResendVerificationCodeTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ShouldReturn202_WhenAccountIsPendingConfirmation()
    {
        // Arrange — register a new person without confirming
        var faker = new Faker("pt_BR");
        var unconfirmedEmail = faker.Internet.Email();

        await Client.PostAsJsonAsync("api/natural-person", new RequestRegisterNaturalPerson
        {
            Email = unconfirmedEmail,
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            FullName = faker.Name.FullName(),
            Journey = Journey.Depositor,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        });

        // Act
        var response = await Client.PostAsJsonAsync("api/account/resend-code",
            new RequestResendVerificationCode { Email = unconfirmedEmail });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Post_ShouldReturnConflict_WhenAccountIsAlreadyActive()
    {
        // Arrange — account confirmed (Active status)
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/resend-code",
            new RequestResendVerificationCode { Email = email });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}

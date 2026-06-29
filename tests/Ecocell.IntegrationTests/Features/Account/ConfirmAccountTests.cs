using System.Net;
using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;

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
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        };
        await Client.PostAsJsonAsync("api/natural-person", registerRequest);

        // Act — wrong code
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = "000000" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenEmailDoesNotExist()
    {
        // Arrange — e-mail sem conta cadastrada; resposta neutra anti-enumeração
        var email = new Faker("pt_BR").Internet.Email();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = "123456" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenAccountAlreadyConfirmed()
    {
        // Arrange — registra, confirma e tenta confirmar novamente (status já Active)
        var (email, _) = await CreateAndLoginNaturalPersonAsync();

        // Act
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = "123456" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn400_WhenPayloadIsInvalid()
    {
        // Act — e-mail malformado e código fora do formato de 6 dígitos
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = "naoemail", Code = "123" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenCodeIsMissing()
    {
        // Arrange — pessoa em AwaitingConfirmation sem código OTP no store (resposta neutra)
        var faker = new Faker("pt_BR");
        var email = faker.Internet.Email();

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var person = new NaturalPerson(
                fullName: faker.Name.FullName(),
                cpf: faker.Person.Cpf(includeFormatSymbols: false),
                birthDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
                role: ApiEnums.Role.User,
                email: email,
                journey: ApiEnums.Journey.Depositor);

            db.People.Add(person);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = "123456" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn403_WhenMaxAttemptsExceeded()
    {
        // Arrange — registra para gerar o código; as tentativas inválidas incrementam o contador
        var faker = new Faker("pt_BR");
        var email = faker.Internet.Email();

        var registerRequest = new RequestRegisterNaturalPerson
        {
            Email = email,
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            FullName = faker.Name.FullName(),
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        };
        await Client.PostAsJsonAsync("api/natural-person", registerRequest);

        // Arrange — esgota o limite com 3 tentativas incorretas
        for (var i = 0; i < 3; i++)
        {
            await Client.PostAsJsonAsync("api/account/confirm",
                new RequestConfirmAccount { Email = email, Code = "000000" });
        }

        // Act — 4ª tentativa atinge o teto de tentativas
        var response = await Client.PostAsJsonAsync("api/account/confirm",
            new RequestConfirmAccount { Email = email, Code = "000000" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

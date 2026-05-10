using System.Net.Http.Json;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.IntegrationTests.Stubs;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFixture Fixture;
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTestBase(IntegrationTestFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        GetCapturingEmailSender().Clear();

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        Client = Fixture.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registra uma pessoa física via endpoint, confirma a conta via OTP e faz login.
    /// Retorna o e-mail gerado e o JWT de acesso.
    /// </summary>
    protected async Task<(string email, string jwt)> CreateAndLoginNaturalPersonAsync(
        SharedEnums.Journey journey = SharedEnums.Journey.Depositor)
    {
        var faker = new Faker("pt_BR");
        var request = new RequestRegisterNaturalPerson
        {
            FullName = faker.Name.FullName(),
            Email = faker.Internet.Email(),
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            Journey = journey,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20))
        };

        var registerResponse = await Client.PostAsJsonAsync("api/natural-person", request);
        registerResponse.EnsureSuccessStatusCode();

        var confirmationCode = GetCapturingEmailSender().GetCapturedCode(request.Email);
        if (confirmationCode is null)
            throw new InvalidOperationException($"Nenhum código de confirmação capturado para {request.Email}.");

        var confirmRequest = new RequestConfirmAccount
        {
            Email = request.Email,
            Code = confirmationCode
        };

        var confirmResponse = await Client.PostAsJsonAsync("api/account/confirm", confirmRequest);
        confirmResponse.EnsureSuccessStatusCode();

        var jwt = await LoginAsync(request.Email);
        return (request.Email, jwt);
    }

    /// <summary>
    /// Cria um administrador diretamente no banco de dados e realiza login.
    /// Retorna o e-mail e o JWT de acesso.
    /// </summary>
    protected async Task<(string email, string jwt)> CreateAdminAndLoginAsync()
    {
        var faker = new Faker("pt_BR");
        var email = faker.Internet.Email();

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var admin = new NaturalPerson(
                fullName: faker.Name.FullName(),
                cpf: faker.Person.Cpf(includeFormatSymbols: false),
                birthDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
                role: ApiEnums.Role.Admin,
                email: email,
                journey: ApiEnums.Journey.None);

            admin.Confirm();

            db.People.Add(admin);
            await db.SaveChangesAsync();
        }

        var jwt = await LoginAsync(email);
        return (email, jwt);
    }

    /// <summary>
    /// Solicita o OTP de login para o e-mail informado, recupera o código do stub
    /// e troca pelo JWT de acesso.
    /// </summary>
    protected async Task<string> LoginAsync(string email)
    {
        var requestCodeRequest = new RequestRequestLoginCode { Email = email };
        var requestCodeResponse = await Client.PostAsJsonAsync("api/account/login/request-code", requestCodeRequest);
        requestCodeResponse.EnsureSuccessStatusCode();

        var otp = GetCapturingEmailSender().GetCapturedCode(email);
        if (otp is null)
            throw new InvalidOperationException($"Nenhum código OTP de login capturado para {email}.");

        var verifyRequest = new RequestVerifyLoginCode
        {
            Email = email,
            Code = otp
        };

        var loginResponse = await Client.PostAsJsonAsync("api/account/login", verifyRequest);
        loginResponse.EnsureSuccessStatusCode();

        var responseLogin = await loginResponse.Content.ReadFromJsonAsync<ResponseLogin>();
        if (responseLogin is null)
            throw new InvalidOperationException("Falha ao desserializar ResponseLogin.");

        return responseLogin.AccessToken;
    }

    /// <summary>
    /// Retorna o stub de e-mail registrado como singleton no contêiner da Factory.
    /// </summary>
    protected CapturingEmailSender GetCapturingEmailSender()
    {
        return (CapturingEmailSender)Fixture.Factory.Services.GetRequiredService<Ecocell.Api.Services.Email.IEmailSender>();
    }

    /// <summary>
    /// Cria um HttpClient com o cabeçalho Authorization: Bearer configurado.
    /// O chamador é responsável por descartar o cliente retornado.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(string jwt)
    {
        var client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    /// <summary>
    /// Semeia uma pessoa jurídica com jornada CollectPoint (status PendingApproval)
    /// diretamente no banco de dados, vinculada ao responsável informado.
    /// </summary>
    protected async Task<LegalPerson> CreatePendingLegalPersonAsync(Guid responsiblePersonId)
    {
        var faker = new Faker("pt_BR");

        var address = new Address(
            street: faker.Address.StreetName(),
            number: faker.Address.BuildingNumber(),
            neighborhood: faker.Address.SecondaryAddress(),
            city: faker.Address.City(),
            state: faker.Address.StateAbbr(),
            zipCode: faker.Address.ZipCode("########"));

        var legalPerson = new LegalPerson(
            legalName: faker.Company.CompanyName(),
            tradeName: faker.Company.CompanyName(),
            cnpj: faker.Company.Cnpj(includeFormatSymbols: false),
            email: faker.Internet.Email(),
            journey: ApiEnums.Journey.CollectPoint,
            responsiblePersonId: responsiblePersonId);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Addresses.Add(address);
        db.People.Add(legalPerson);
        await db.SaveChangesAsync();

        return legalPerson;
    }
}

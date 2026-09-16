using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Requests.Discards;
using Ecocell.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features.Discard;

[Collection(nameof(IntegrationTestCollection))]
public class RegisterDiscardTests : IntegrationTestBase
{
    private static SharedEnums.ElectronicMaterial Material =>
        Enum.GetValues<SharedEnums.ElectronicMaterial>()
            .First(value => Convert.ToInt32(value) > 0);

    public RegisterDiscardTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private static RequestRegisterDiscardJson ValidRequest(Guid pointId) =>
        new()
        {
            QrCode = $"ecocell://pc/{pointId:D}",
            Items =
            [
                new RequestRegisterDiscardItemJson
                {
                    Material = Material,
                    Quantity = 2,
                    ApproximateWeightKg = 0.450m,
                },
            ],
        };

    private async Task AddRuleAsync(Guid pointId)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MaterialScoreRules.Add(
            new MaterialScoreRule(
                pointId,
                (ApiEnums.ElectronicMaterial)(int)Material,
                10m,
                ApiEnums.MaterialScoreUnit.PerUnit,
                DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();
    }

    private async Task<int> CountDiscardsAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Discards.CountAsync();
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenTokenIsMissing()
    {
        // Act
        var response = await Client.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn201AndPersistDiscard_WhenRequestIsValid()
    {
        // Arrange
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var depositorId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(depositorId);
        await AddRuleAsync(point.Id);
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(point.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ResponseRegisterDiscardJson>();
        body.ShouldNotBeNull();
        body!.Status.ShouldBe(SharedEnums.DiscardStatus.Pending);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var discard = await db.Discards.Include(value => value.Items).SingleAsync();

        discard.Id.ShouldBe(body.Id);
        discard.DepositorId.ShouldBe(depositorId);
        discard.CollectorPointId.ShouldBe(point.Id);
        discard.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Post_ShouldReturn400_WhenQrCodeIsInvalid()
    {
        // Arrange
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);
        var request = ValidRequest(Guid.NewGuid()) with { QrCode = "invalid" };

        // Act
        var response = await authClient.PostAsJsonAsync("api/v1/discards", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CountDiscardsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Post_ShouldReturn403_WhenPersonIsNotDepositor()
    {
        // Arrange
        var (_, jwt) = await CreateAdminAndLoginAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountDiscardsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Post_ShouldReturn404_WhenCollectorPointDoesNotExist()
    {
        // Arrange
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CountDiscardsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenCollectorPointIsInactive()
    {
        // Arrange
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var depositorId = await GetPersonIdByEmailAsync(email);
        var point = await CreatePendingLegalPersonAsync(depositorId);
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(point.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CountDiscardsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenMaterialIsNotAccepted()
    {
        // Arrange
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var depositorId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(depositorId);
        using var authClient = CreateAuthenticatedClient(jwt);

        // Act
        var response = await authClient.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(point.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CountDiscardsAsync()).ShouldBe(0);
    }
}

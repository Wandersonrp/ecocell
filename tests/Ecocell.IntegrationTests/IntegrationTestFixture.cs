using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.External;
using Ecocell.IntegrationTests.Stubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Ecocell.IntegrationTests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("ecocell_test")
        .WithUsername("ecocell")
        .WithPassword("ecocell_test_pass")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                    ["Redis:ConnectionString"] = _redis.GetConnectionString(),
                    ["Jwt:SigningKey"] = "ecocell-integration-test-signing-key-2026!!",
                    ["Jwt:Issuer"] = "ecocell-test",
                    ["Jwt:Audience"] = "ecocell-test",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "60",
                    ["Jwt:RefreshTokenLifetimeDays"] = "7",
                    ["Nominatim:BaseUrl"] = "http://localhost/",
                    ["Nominatim:UserAgent"] = "ecocell-integration-test",
                    ["Nominatim:TimeoutSeconds"] = "5",
                    ["Mail:Host"] = "localhost",
                    ["Mail:Port"] = "25",
                    ["Mail:Username"] = "test",
                    ["Mail:Password"] = "test",
                    ["Mail:From"] = "test@test.com",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Substitui IEmailSender pelo stub capturador (Singleton para que o mesmo
                // instância seja resolvida tanto pelo handler quanto pelos testes)
                var emailDescriptors = services
                    .Where(d => d.ServiceType == typeof(IEmailSender))
                    .ToList();
                foreach (var d in emailDescriptors) services.Remove(d);
                services.AddSingleton<IEmailSender, CapturingEmailSender>();

                // Substitui IGeocodingService pelo stub que retorna coordenadas fixas
                var geoDescriptors = services
                    .Where(d => d.ServiceType == typeof(IGeocodingService))
                    .ToList();
                foreach (var d in geoDescriptors) services.Remove(d);
                services.AddSingleton<IGeocodingService, StubGeocodingService>();
            });
        });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}

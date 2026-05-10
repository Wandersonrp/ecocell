using Ecocell.Api.Database;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.External;
using Ecocell.IntegrationTests.Stubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
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

            builder.ConfigureServices(services =>
            {
                // Substitui AppDbContext pelo container Postgres dinâmico
                var dbDescriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                             || d.ServiceType == typeof(AppDbContext))
                    .ToList();
                foreach (var d in dbDescriptors) services.Remove(d);
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_postgres.GetConnectionString()));

                // Substitui IConnectionMultiplexer pelo container Redis dinâmico
                var redisDescriptors = services
                    .Where(d => d.ServiceType == typeof(IConnectionMultiplexer))
                    .ToList();
                foreach (var d in redisDescriptors) services.Remove(d);
                services.AddSingleton<IConnectionMultiplexer>(_ =>
                    ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

                // Substitui IEmailSender pelo stub capturador (Singleton para que a mesma
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

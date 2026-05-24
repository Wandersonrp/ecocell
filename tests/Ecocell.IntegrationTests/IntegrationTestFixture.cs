using Ecocell.Api.Database;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.External;
using Ecocell.IntegrationTests.Stubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    public string _postgresConnectionString => _postgres.GetConnectionString();
    public string _redisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(ConfigureTestServices);
        });
    }

    /// <summary>
    /// Cria uma WebApplicationFactory com rate limiting ativo e limite reduzido,
    /// reutilizando os containers Postgres e Redis da fixture principal.
    /// O PermitLimit é aplicado via ConfigureAppConfiguration, que roda antes de
    /// AddApi ser chamado no Program.cs, garantindo que a política use o novo limite.
    /// </summary>
    public WebApplicationFactory<Program> CreateRateLimitedFactory(int permitLimit = 2)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(cfg =>
            {
                // Ativa rate limiting com PermitLimit reduzido. Sobrescreve
                // appsettings.Testing.json, que mantém Enabled=false para não
                // afetar os demais testes de integração.
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Enabled"] = "true",
                    ["RateLimit:PermitLimit"] = permitLimit.ToString(),
                    ["RateLimit:WindowMinutes"] = "1"
                });
            });
            builder.ConfigureServices(ConfigureTestServices);
        });
    }

    private void ConfigureTestServices(IServiceCollection services)
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
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
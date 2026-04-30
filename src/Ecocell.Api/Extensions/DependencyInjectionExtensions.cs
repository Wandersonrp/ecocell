using System.Text;
using Carter;
using Ecocell.Api.Configurations;
using Ecocell.Api.Enums;
using Ecocell.Api.Shared;
using Ecocell.Api.Database;
using Ecocell.Api.Services.Authentication;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Services.Email;
using Ecocell.Api.Services.VerificationCodes;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;

namespace Ecocell.Api.Extensions;

/// <summary>
/// Extensões de composição da injeção de dependência da API EcoCell.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Registra todos os serviços necessários para a execução da API.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configurações da aplicação.</param>
    public static void AddApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ConfigLog();
        AddMediator(services);
        AddDbContext(services, configuration);
        AddCarter(services);
        AddSettings(services, configuration);
        AddRedis(services, configuration);
        AddServices(services, environment);
        AddJwtAuthentication(services, configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        var assembly = typeof(Program).Assembly;
        services.AddValidatorsFromAssembly(assembly);
    }

    private static void ConfigLog()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{Exception}{NewLine}")
            .CreateLogger();
    }

    private static void AddMediator(IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = configuration
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>();

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(dbSettings!.DefaultConnection);
        });
    }

    private static void AddCarter(IServiceCollection services) => services.AddCarter();

    private static void AddSettings(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisSettings>()
            .Bind(configuration.GetSection(RedisSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<MailSettings>(configuration.GetSection(MailSettings.SectionName));
    }

    /// <summary>
    /// Registra a conexão Redis e o store de códigos OTP.
    /// Requer a chave "Redis:ConnectionString" em appsettings ou User Secrets.
    /// Em desenvolvimento local: docker run -d -p 6379:6379 redis:7-alpine
    /// </summary>
    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(RedisSettings.SectionName)
            .Get<RedisSettings>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(settings!.ConnectionString));

        services.AddSingleton<IVerificationCodeStore, RedisVerificationCodeStore>();
    }

    /// <summary>
    /// Registra os serviços transversais da API (e-mail, integrações externas).
    /// </summary>
    private static void AddServices(IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsProduction() || environment.IsStaging())
            services.AddScoped<IEmailSender, MailKitEmailSender>();
        else
            services.AddSingleton<IEmailSender, LoggingEmailSender>();

        services.AddSingleton<IJwtTokenService, JwtTokenService>();
    }

    /// <summary>
    /// Registra autenticação JWT Bearer e autorização.
    /// Requer a chave "Jwt:SigningKey" em User Secrets ou variável de ambiente.
    /// </summary>
    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException("Seção 'Jwt' não encontrada em appsettings.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings!.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.Authenticated, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(AuthorizationPolicies.Admin, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("role", Ecocell.Api.Enums.Role.Admin.ToString()));
        });
    }
}

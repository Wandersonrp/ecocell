using Carter;
using Ecocell.Api.Configurations;
using Ecocell.Api.Database;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Ecocell.Api.Extensions;

public static class DependencyInjectionExtensions
{
    public static void AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        ConfigLog();
        AddMediator(services);
        AddDbContext(services, configuration);
        AddCarter(services);
        AddSettings(services, configuration);

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
    }
}

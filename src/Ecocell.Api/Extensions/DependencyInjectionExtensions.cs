using Ecocell.Api.Database;
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
        services.AddMediator(options => options.Assemblies = [typeof(Program)]);
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        });
    }
}

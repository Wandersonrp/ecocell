using Ecocell.Infrastructure.Data.Context;
using Ecocell.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecocell.Infrastructure;

public static class Bootstrapper
{
    public static void InitializeInfra(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EcocellDbContext>(options =>
        {
            options.UseNpgsql(services.GetConnectionString(configuration));
        });
    }
}

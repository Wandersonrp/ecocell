using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecocell.Application;

public static class Bootstrapper
{
    public static void InitializeApplication(this IServiceCollection services, IConfiguration configuration)
    {
        Infrastructure
            .Bootstrapper
            .InitializeInfra(services, configuration);
    }
}

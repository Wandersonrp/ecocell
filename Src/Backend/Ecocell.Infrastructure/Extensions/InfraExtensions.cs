using Ecocell.Exception;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecocell.Infrastructure.Extensions;

public static class InfraExtensions
{
    public static string GetConnectionString(
        this IServiceCollection service, 
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
            throw new ArgumentNullException(ResourceErrorMessages.CONNECTION_STRING_NOT_FOUND);

        return connectionString;
    }
}

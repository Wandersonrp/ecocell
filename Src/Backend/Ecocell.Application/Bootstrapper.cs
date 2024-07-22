using Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;
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

        AddUseCases(services);
    }

    private static void AddUseCases(IServiceCollection services)
    {
        services
            .AddScoped<IRegisterNaturalPerson, RegisterNaturalPersonUseCase>();
    }
}

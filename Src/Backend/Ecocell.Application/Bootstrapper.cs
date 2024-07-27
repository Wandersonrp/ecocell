using AutoMapper;
using Ecocell.Application.Services.AutoMapper;
using Ecocell.Application.UseCases.Users.LegalPerson.RegisterLegalPerson;
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
        AddAutoMapper(services);
    }

    private static void AddUseCases(IServiceCollection services)
    {
        services
            .AddScoped<IRegisterNaturalPerson, RegisterNaturalPersonUseCase>()
            .AddScoped<IRegisterLegalPerson, RegisterLegalPersonUseCase>();
    }

    private static void AddAutoMapper(IServiceCollection services)
    {
        services.AddScoped(options => new MapperConfiguration(options =>
        {
            options.AddProfile(new AutoMapping());
        }).CreateMapper());
    }
}

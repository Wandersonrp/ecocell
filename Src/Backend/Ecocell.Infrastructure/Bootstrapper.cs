using Ecocell.Domain.Repositories;
using Ecocell.Domain.Repositories.Users.LegalPerson;
using Ecocell.Domain.Repositories.Users.NaturalPerson;
using Ecocell.Domain.Services.Cryptography;
using Ecocell.Infrastructure.Data;
using Ecocell.Infrastructure.Data.Context;
using Ecocell.Infrastructure.Data.Repositories.Users.LegalPerson;
using Ecocell.Infrastructure.Data.Repositories.Users.NaturalPerson;
using Ecocell.Infrastructure.Extensions;
using Ecocell.Infrastructure.Services.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecocell.Infrastructure;

public static class Bootstrapper
{
    public static void InitializeInfra(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        AddRepositories(services);
        AddUnitOfWork(services);
        AddServices(services);
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EcocellDbContext>(options =>
        {
            options.UseNpgsql(services.GetConnectionString(configuration));
        });
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services
            .AddScoped<INaturalPersonReadOnlyRepository, NaturalPersonRepository>()
            .AddScoped<INaturalPersonWriteOnlyRepository, NaturalPersonRepository>()
            .AddScoped<ILegalPersonReadOnlyRepository, LegalPersonRepository>()
            .AddScoped<ILegalPersonWriteOnlyRepository, LegalPersonRepository>();
    }

    private static void AddUnitOfWork(IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void AddServices(IServiceCollection services)
    {
        services
            .AddScoped<IHashGenerator, HashService>()
            .AddScoped<IHashComparer, HashService>();
            //.AddScoped<ILegalPersonChecker<ResponseBrazilApiCompanyDataJson>, BrazilApiCompanyChecker>()
            //.AddScoped<IGeocoding<ResponseNominatimGeocodingJson>, NominatimGeocoding>();
    }
}

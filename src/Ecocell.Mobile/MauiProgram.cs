using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Refit;
using Ecocell.Mobile.Services.Api;
using Ecocell.Mobile.Services.Auth;

namespace Ecocell.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-VariableFont.ttf", "Inter");
            });

        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("Ecocell.Mobile.appsettings.json");
        if (stream is not null)
            builder.Configuration.AddJsonStream(stream);

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]!;
        builder.Services.AddRefitClient<IEcocellApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

        builder.Services.AddRefitClient<IAccountClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

        builder.Services.AddRefitClient<IMapClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<AuthTokenHandler>();

        builder.Services.AddRefitClient<ILegalPersonClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<AuthTokenHandler>();

        builder.Services.AddRefitClient<IAdminClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<AuthTokenHandler>();

        builder.Services.AddTransient<Ecocell.Mobile.ViewModels.LegalPersonViewModel>();

        builder.Services.AddSingleton<ISecureTokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<AuthStateService>();
        builder.Services.AddTransient<AuthTokenHandler>();
        builder.Services.AddSingleton<Ecocell.Mobile.Services.Location.ILocationService,
                                      Ecocell.Mobile.Services.Location.LocationService>();
        builder.Services.AddTransient<Ecocell.Mobile.ViewModels.MapViewModel>();

        // Client dedicado ao refresh — registrado SEM AuthTokenHandler (evita ciclo/recursão).
        builder.Services.AddRefitClient<IAuthRefreshClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

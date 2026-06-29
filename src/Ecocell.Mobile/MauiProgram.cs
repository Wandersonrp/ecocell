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
        using var stream = assembly.GetManifestResourceStream("Ecocell.Mobile.appsettings.json");
        builder.Configuration.AddJsonStream(stream!);

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]!;
        builder.Services.AddRefitClient<IEcocellApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

        builder.Services.AddRefitClient<IAccountClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

        builder.Services.AddSingleton<ISecureTokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<AuthStateService>();
        builder.Services.AddTransient<AuthTokenHandler>();

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

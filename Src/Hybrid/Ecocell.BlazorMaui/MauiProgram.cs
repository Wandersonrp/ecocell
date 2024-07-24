using Android.App;
using Ecocell.Razor.Services.Users.Register;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Ecocell.BlazorMaui;

#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.Services.AddMudServices();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var apiDevUrl = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5000" : "http://localhost:5150/api";

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiDevUrl) });

        builder.Services.AddScoped<IRegisterUserService, RegisterUserService>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

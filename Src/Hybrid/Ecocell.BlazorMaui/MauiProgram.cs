using Ecocell.Razor.Services.Users.Register;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Ecocell.BlazorMaui;

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

        var apiDevUrl = "http://localhost:5150";

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiDevUrl) });

        builder.Services.AddScoped<IRegisterUserService, RegisterUserService>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

using Ecocell.Communication.Responses.BrazilApiCompanyData;
using Ecocell.Razor.Services.LegalPersonChecker;
using Ecocell.Razor.Services.Users.Register;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using MudExtensions.Services;

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
                
        builder.Services.AddHttpClient();

        builder.Services.AddScoped<ILegalPersonChecker<ResponseBrazilApiCompanyDataJson>, BrazilApiCompanyChecker>();

        builder.Services.AddScoped<IRegisterUserService, RegisterUserService>();

        builder.Services.AddMudExtensions();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

using Ecocell.Mobile.Services.Auth;
using Ecocell.Mobile.Services.Notifications;
#if ANDROID
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
#endif

namespace Ecocell.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly PendingDiscardMonitor _pendingMonitor;

    public App(AuthStateService authState, PendingDiscardMonitor pendingMonitor)
    {
        _pendingMonitor = pendingMonitor;
        InitializeComponent();

#if ANDROID
        // O MAUI sobrescreve o windowSoftInputMode do manifest em runtime com AdjustPan
        // (herança do Forms). Sem esta chamada, o teclado cobre inputs do BlazorWebView
        // porque o pan não reage a foco de elemento web (WND-272).
        this.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
            .UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
#endif

        // Carrega o par de tokens persistido antes da 1ª renderização.
        // Ambos os estados (logado/deslogado) abrem em "/" — sem mudança de rota.
        _ = authState.InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Ecocell.Mobile" };
        window.Activated += (_, _) => _pendingMonitor.SetForeground(true);
        window.Resumed += (_, _) => _pendingMonitor.SetForeground(true);
        window.Deactivated += (_, _) => _pendingMonitor.SetForeground(false);
        window.Stopped += (_, _) => _pendingMonitor.SetForeground(false);
        return window;
    }
}

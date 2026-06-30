using Ecocell.Mobile.Services.Auth;

namespace Ecocell.Mobile;

public partial class App : Application
{
    public App(AuthStateService authState)
    {
        InitializeComponent();

        // Carrega o par de tokens persistido antes da 1ª renderização.
        // Ambos os estados (logado/deslogado) abrem em "/" — sem mudança de rota.
        _ = authState.InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Ecocell.Mobile" };
    }
}

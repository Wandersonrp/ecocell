using Ecocell.Mobile.Components.Shared.Navigation;
using MudBlazor;

namespace Ecocell.Mobile.Services.Navigation;

public static class DepositorNav
{
    public static readonly IReadOnlyList<EcoNavItem> Items =
    [
        new("inicio", "Início", Icons.Material.Filled.Home, "/map"),
        new("descartar", "Descartar", Icons.Material.Filled.Recycling, "/descartes/escanear"),
        new("ranking", "Ranking", Icons.Material.Filled.EmojiEvents),
        new("perfil", "Perfil", Icons.Material.Filled.Person, "/perfil"),
    ];
}

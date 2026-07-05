using Ecocell.Mobile.Components.Shared.Navigation;
using Ecocell.Mobile.Services.Context;
using MudBlazor;

namespace Ecocell.Mobile.Services.Navigation;

/// <summary>Ids estáveis dos itens de nav do modo PC (usados como ActiveId nas telas).</summary>
public static class NavIds
{
    public const string CollectorPointHome = "pc-inicio";
    public const string Materials = "pc-materiais";
    public const string Coupons = "pc-cupons";
    public const string Seals = "pc-selos";
    public const string Profile = "pc-perfil";
}

/// <summary>
/// Resolve os itens da nav-bar conforme o contexto ativo. Itens do modo PC cujas telas
/// ainda não existem entram como inertes (sem Href) - as telas os ativam ao serem criadas.
/// </summary>
public static class NavItemsResolver
{
    private static readonly IReadOnlyList<EcoNavItem> CollectPointItems =
    [
        new(NavIds.CollectorPointHome, "Início", Icons.Material.Filled.Storefront),
        new(NavIds.Materials, "Materiais", Icons.Material.Filled.Category),
        new(NavIds.Coupons, "Cupons", Icons.Material.Filled.ConfirmationNumber),
        new(NavIds.Seals, "Selos", Icons.Material.Filled.WorkspacePremium),
        new(NavIds.Profile, "Perfil", Icons.Material.Filled.Person),
    ];

    public static IReadOnlyList<EcoNavItem> Resolve(ActiveContext context) =>
        context.IsCollectPoint ? CollectPointItems : [];
}

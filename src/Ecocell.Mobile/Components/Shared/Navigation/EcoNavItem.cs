namespace Ecocell.Mobile.Components.Shared.Navigation;

/// <summary>Item da barra de navegação inferior. <c>Icon</c> é uma classe de ícone MudBlazor (Material).</summary>
public record EcoNavItem(string Id, string Label, string Icon, string? Href = null);

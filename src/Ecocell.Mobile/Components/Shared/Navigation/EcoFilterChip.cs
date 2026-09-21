namespace Ecocell.Mobile.Components.Shared.Navigation;

/// <summary>Opção de <c>EcoFilterChips</c>. <c>Count</c> opcional exibe um contador no chip.</summary>
public record EcoFilterChip(string Id, string Label, string? Count = null);

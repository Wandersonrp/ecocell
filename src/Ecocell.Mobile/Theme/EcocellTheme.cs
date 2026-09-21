using MudBlazor;

namespace Ecocell.Mobile.Theme;

public static class EcocellTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = ThemeTokens.ColorPrimary,
            PrimaryContrastText = ThemeTokens.ColorTextOnPrimary,
            Secondary = ThemeTokens.ColorPrimaryLight,
            SecondaryContrastText = ThemeTokens.ColorTextOnPrimary,
            Background = ThemeTokens.ColorSurface,
            Surface = ThemeTokens.ColorSurface,
            Error = ThemeTokens.ColorError,
            ErrorContrastText = ThemeTokens.ColorTextOnPrimary,
            TextPrimary = ThemeTokens.ColorTextPrimary,
            TextSecondary = ThemeTokens.ColorTextSecondary,
            ActionDefault = ThemeTokens.ColorTextTertiary,
            ActionDisabled = ThemeTokens.ColorTextDisabled,
            ActionDisabledBackground = ThemeTokens.ColorPrimaryTint,
            Divider = ThemeTokens.ColorDivider,
            DividerLight = ThemeTokens.ColorDivider,
            LinesDefault = ThemeTokens.ColorBorder,
            LinesInputs = ThemeTokens.ColorBorder,
            AppbarBackground = ThemeTokens.ColorSurface,
            AppbarText = ThemeTokens.ColorTextPrimary,
            DrawerBackground = ThemeTokens.ColorSurface,
            DrawerText = ThemeTokens.ColorTextPrimary,
            TableHover = ThemeTokens.ColorPrimaryTint,
            TableLines = ThemeTokens.ColorDivider,
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = [ThemeTokens.FontFamily, "sans-serif"],
            },
        },
    };
}

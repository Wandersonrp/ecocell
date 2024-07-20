using MudBlazor;

namespace Ecocell.Razor.Customization.Themes;

public class DefaultTheme
{
    public MudTheme MudTheme { get; set; } = null!;

    public DefaultTheme()
    {
        MudTheme = new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                TextPrimary = Tokens.Color.GreenDarken4,
                Background = Tokens.Color.GreyLighten5,
                Black = Tokens.Color.Black,
                AppbarBackground = Tokens.Color.GreenDarken4,
                AppbarText = Tokens.Color.White,
                Primary = Tokens.Color.GreenDarken4,
                Secondary = Tokens.Color.White,
                Tertiary = Tokens.Color.GreyLighten2,                
                White = Tokens.Color.White,
            }
        };
    }
}

using Ecocell.Communication.Enums.User;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ecocell.Razor.Components.Switches;

public partial class SwitchButtons
{
    [Parameter]
    public string ButtonLeftText { get; set; } = "ButtonLeft";

    [Parameter]
    public string ButtonRightText { get; set; } = "ButtonRight";

    private Color _buttonLeftColor = Color.Secondary;
    private Color _buttonRightColor = Color.Tertiary;

    private bool _buttonLeftDropShadow = true;
    private bool _buttonRightDropShadow = false;

    private string _buttonLeftText = Customization.Tokens.Color.Black;
    private string _buttonRightText = Customization.Tokens.Color.GreyLighten1;

    [Parameter]
    public UserType UserType { get; set; } = UserType.NaturalPerson;

    [Parameter]
    public EventCallback<UserType> UserTypeChanged { get; set; }

    private async Task OnButtonLeftClick()
    {
        _buttonLeftDropShadow = true;
        _buttonRightDropShadow = false;

        _buttonLeftText = Customization.Tokens.Color.Black;
        _buttonRightText = Customization.Tokens.Color.GreyLighten1;

        _buttonRightColor = Color.Tertiary;
        _buttonLeftColor = Color.Secondary;

        await OnUserTypeChanged(UserType.NaturalPerson);

        StateHasChanged();
    }

    private async Task OnButtonRightClick()
    {
        _buttonLeftDropShadow = false;
        _buttonRightDropShadow = true;

        _buttonLeftText = Customization.Tokens.Color.GreyLighten1;
        _buttonRightText = Customization.Tokens.Color.Black;

        _buttonRightColor = Color.Secondary;
        _buttonLeftColor = Color.Tertiary;

        UserType = UserType.LegalPerson;

        await OnUserTypeChanged(UserType.LegalPerson);

        StateHasChanged();
    }

    private async Task OnUserTypeChanged(UserType userType)
    {
        UserType = userType;

        await UserTypeChanged.InvokeAsync(userType);
    }
}
using Ecocell.Communication.Enums.User;

namespace Ecocell.Razor.Pages.Register;

public partial class RegisterPage
{
    private UserType _userType { get; set; } = UserType.NaturalPerson;

    private void OnUserTypeChanged(UserType userType)
    {
        _userType = userType;
    }
}

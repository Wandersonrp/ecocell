using MudBlazor;
using System.Text.RegularExpressions;

namespace Ecocell.Razor.Components.RegisterForm.NaturalPerson;

public partial class RegisterFormNaturalPerson
{
    bool success;
    string[] errors = { };
    MudTextField<string> pwField1;
    MudForm form;

    private IEnumerable<string> PasswordStrength(string pw)
    {
        if (string.IsNullOrWhiteSpace(pw))
        {
            yield return "Senha é obrigatória!";
            yield break;
        }
        if (pw.Length < 8)
            yield return "Password must be at least of length 8";
        if (!Regex.IsMatch(pw, @"[A-Z]"))
            yield return "Password must contain at least one capital letter";
        if (!Regex.IsMatch(pw, @"[a-z]"))
            yield return "Password must contain at least one lowercase letter";
        if (!Regex.IsMatch(pw, @"[0-9]"))
            yield return "Password must contain at least one digit";
    }

    private string PasswordMatch(string arg)
    {
        if (pwField1.Value != arg)
            return "Passwords don't match";
        return null;
    }
}

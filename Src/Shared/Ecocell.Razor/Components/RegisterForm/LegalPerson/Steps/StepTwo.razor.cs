using Ecocell.Communication.Requests.Address;
using Ecocell.Communication.Requests.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ecocell.Razor.Components.RegisterForm.LegalPerson.Steps;

public partial class StepTwo
{
    private MudForm _formStepTwo = new();
    private bool _error;

    [Parameter]
    public RequestRegisterUserJson User { get; set; }

    [Parameter]
    public RequestAddressJson Address { get; set; } = new();

    private PatternMask _zipCodeMask = new PatternMask("XXXXX-XXX")
    {
        MaskChars = new[] { new MaskChar('X', @"[0-9]") },
        Placeholder = '_',
        CleanDelimiters = true,
    };
}

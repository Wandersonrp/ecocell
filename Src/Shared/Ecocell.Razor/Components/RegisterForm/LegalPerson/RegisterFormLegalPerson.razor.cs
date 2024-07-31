using Ecocell.Communication.Enums.Document;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Address;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using MudBlazor;
using MudExtensions;
using ZXing.QrCode.Internal;

namespace Ecocell.Razor.Components.RegisterForm.LegalPerson;

public partial class RegisterFormLegalPerson
{
    private MudStepperExtended? _stepper = new();
    private MudForm _form = new();
    private MudForm _form2 = new();
    private bool _checkValidationBeforeComplete = false;
    private bool _linear;
    private bool _mobileView;
    private bool _addResultStep = true;
    private bool _loading;
    private bool _vertical = false;
    private int _infoStepOrder;
    private int _optionsStepOrder;
    private int _paymentStepOrder;
    private bool _isSubmitLoading;
    private RequestDocumentJson _document = new();
    private RequestAddressJson _address { get; set; } = new();
    private RequestRegisterUserJson _user { get; set; } = new();

    private void OnAddressChanged(RequestAddressJson address)
    {
        _address = address;
    }

    private void OnUserChanged(RequestRegisterUserJson user)
    {
        _user = user;
    }

    private async Task Submit()
    {
        try
        {
            _isSubmitLoading = true;                      

            //await _form.Validate();

            if (true)
            {
                await RegisterUserService.RegisterUser(_user);
            }
        }
        catch (System.Exception ex)
        {
            Console.Out.WriteLine($"Ex.: {ex.Message}");
        }

        _isSubmitLoading = false;

        StateHasChanged();
    }

    private async Task<bool> CheckChange(StepChangeDirection direction, int targetIndex)
    {
        if (_checkValidationBeforeComplete == true)
        {
            // Always allow stepping backwards, even if forms are invalid
            if (direction == StepChangeDirection.Backward)
            {
                return false;
            }
            if (_stepper?.GetActiveIndex() == 0)
            {
                _loading = true;
                StateHasChanged();
                await _form.Validate();
                _loading = false;
                StateHasChanged();
                return !_form.IsValid;
            }
            else if (_stepper?.GetActiveIndex() == 1)
            {
                _loading = true;
                StateHasChanged();
                await _form2.Validate();
                _loading = false;
                StateHasChanged();
                return !_form2.IsValid;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }    
}

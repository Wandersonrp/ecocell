using Ecocell.Communication.Requests.Address;
using MudBlazor;
using MudExtensions;

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
    private RequestAddressJson _address { get; set; } = new();

    private void OnAddressChanged(RequestAddressJson address)
    {
        _address = address;
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
                await Task.Delay(100);
                await _form.Validate();
                _loading = false;
                StateHasChanged();
                return !_form.IsValid;
            }
            else if (_stepper?.GetActiveIndex() == 2)
            {
                _loading = true;
                StateHasChanged();
                await Task.Delay(100);
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

    private void StatusChanged(StepStatus status)
    {
        Snackbar.Add($"First step {status.ToDescriptionString()}.", Severity.Info);
    }
}

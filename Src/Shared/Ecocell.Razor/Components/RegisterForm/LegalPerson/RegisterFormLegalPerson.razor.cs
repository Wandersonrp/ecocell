using Ecocell.Communication.Requests.Address;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using FluentValidation;
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

            _user.Address = _address;

            await Task.WhenAll(_form.Validate(), _form2.Validate());

            if (_form.IsValid && _form2.IsValid)
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

    public class RegisterUserFluentValidator : AbstractValidator<RequestRegisterUserJson> 
    {
        public RegisterUserFluentValidator()
        {
            RuleFor(e => e.Name)
                .NotNull()
                .NotEmpty()
                .WithMessage("Nome fantasia é obrigatório");

            RuleFor(e => e.CorporateName)
                .NotNull()
                .NotEmpty()
                .WithMessage("Razão social é obrigatório");

            RuleFor(e => e.Email)
                .EmailAddress()
                .WithMessage("E-mail inválido");

            RuleFor(e => e.Document.Text)
                .NotNull()
                .NotEmpty()
                .WithMessage("CNPJ é obrigatório");

            RuleFor(e => e.PrincipalCnae)               
               .NotNull()
               .NotEmpty()
               .WithMessage("Cnae principal é obrigatório");

            RuleFor(e => e.Address!.ZipCode)
                .Cascade(CascadeMode.Stop)
                .Length(8)
                .WithMessage("CEP deve ter 8 caracteres")
                .Matches(@"^\d{8}$")
                .WithMessage("CEP deve conter apenas número");            
            
            RuleFor(e => e.Address!.Neighborhood)
                .NotNull()
                .NotEmpty()
                .WithMessage("Bairro é obrigatório");

            RuleFor(e => e.Address!.City)
                .NotNull()
                .NotEmpty()
                .WithMessage("Cidade é obrigatório");

            RuleFor(e => e.Address!.Street)
                .NotNull()
                .NotEmpty()
                .WithMessage("Logradouro é obrigatório");

            RuleFor(e => e.Address!.State)
                .NotNull()
                .NotEmpty()
                .WithMessage("Estado é obrigatório");

            RuleFor(e => e.Password)
                .Cascade(CascadeMode.Stop)
                .MinimumLength(8)
                .WithMessage("Senha deve conter no mínimo 08 caracteres")
                .MaximumLength(20)
                .WithMessage("Senha deve conter no máximo 20 caracteres");

            RuleFor(e => e.PasswordConfirmation)
                .Cascade(CascadeMode.Stop)
                .MinimumLength(8)
                .WithMessage("Confirmação de senha deve conter no mínimo 08 caracteres")
                .MaximumLength(20)
                .WithMessage("Confirmação de senha deve conter no máximo 20 caracteres")
                .Equal(e => e.Password)
                .WithMessage("Confirmação de senha deve ser igual à senha");                                            
        }

        public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
        {
            var result = await ValidateAsync(ValidationContext<RequestRegisterUserJson>.CreateWithOptions((RequestRegisterUserJson)model, x => x.IncludeProperties(propertyName)));

            if (result.IsValid)
            {
                return Array.Empty<string>();
            }

            return result.Errors.Select(e => e.ErrorMessage);
        };
    }
}

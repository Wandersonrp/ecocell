using Ecocell.Communication.Enums.Document;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using FluentValidation;
using MudBlazor;

namespace Ecocell.Razor.Components.RegisterForm.NaturalPerson;

public partial class RegisterFormNaturalPerson
{    
    string[] errors = { };    
    private MudForm _form = new();    
    private RequestRegisterUserJson _model = new();    
    private RegisterUserFluentValidator _validator = new();   
    private bool _isLoading;
    private RequestDocumentJson _document = new();

    private PatternMask _cpfMask = new PatternMask("XXX.XXX.XXX-XX")
    {
        MaskChars = new[] { new MaskChar('X', @"[0-9]") },
        Placeholder = '_',
        CleanDelimiters = true,      
    };

    private async Task Submit()
    {
        try
        {
            _isLoading = true;

            _model.Document = _document;

            _model.Document.DocumentType = DocumentType.CPF;

            _model.UserType = UserType.NaturalPerson;

            await _form.Validate();

            if(_form.IsValid)
            {
                await RegisterUserService.RegisterUser(_model);
            }            
        } 
        catch(System.Exception ex)
        {
            Console.Out.WriteLine($"Ex.: {ex.Message}");
        }

        _isLoading = false;

        StateHasChanged();
    }

    public class RegisterUserFluentValidator : AbstractValidator<RequestRegisterUserJson>
    {       
        public RegisterUserFluentValidator()
        {            
            RuleFor(e => e.Name)
                .NotNull()
                .NotEmpty()
                .WithMessage("Nome é obrigatório");

            RuleFor(e => e.Email)
                .Cascade(CascadeMode.Stop)                
                .EmailAddress()
                .WithMessage("E-mail inválido");

            // TODO: Verificar validador de cpf e cnpj
            RuleFor(e => e.Document.Text)
                .NotNull()
                .NotEmpty()
                .WithMessage("CPF é obrigatório");           

            RuleFor(e => e.Password)                
                .MinimumLength(8)
                .WithMessage("Senha deve conter no mínimo 08 caracteres");

            RuleFor(e => e.Password)                
                .MaximumLength(20)
                .WithMessage("Senha deve conter no máximo 20 caracteres");

            RuleFor(e => e.PasswordConfirmation)                
                .MinimumLength(8)
                .WithMessage("Confirmação de senha deve conter no mínimo 08 caracteres");

            RuleFor(e => e.PasswordConfirmation)                
                .MaximumLength(20)
                .WithMessage("Confirmação de senha deve conter no máximo 20 caracteres");

            RuleFor(e => e.PasswordConfirmation)
                .NotEmpty()
                .NotNull()
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

using Ecocell.Communication.Enums.Document;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using Ecocell.Exception;
using FluentValidation;
using MudBlazor;

namespace Ecocell.Razor.Components.RegisterForm.NaturalPerson;

public partial class RegisterFormNaturalPerson
{    
    string[] errors = { };    
    private MudForm _form = new();    
    private RequestRegisterUserJson _model = new();
    private RegisterUserFluentValidator _validator = new();
    private string _documentText { get; set; } = string.Empty;

    private async Task Submit()
    {
        try
        {
            _model.Document = new RequestDocumentJson
            { 
                Text = _documentText,
                DocumentType = _model.UserType == UserType.LegalPerson ? DocumentType.CNPJ : DocumentType.CPF
            };

            _model.UserType = UserType.NaturalPerson;

            await RegisterUserService.RegisterUser(_model);
        } 
        catch(System.Exception ex)
        {
            Console.Out.WriteLine($"Ex.: {ex.Message}");
        }
    }

    public class RegisterUserFluentValidator : AbstractValidator<RequestRegisterUserJson>
    {
        public RegisterUserFluentValidator()
        {
            RuleFor(e => e.Name)
                .NotNull()
                .WithMessage($"{ResourceErrorMessages.NOT_NULL_ERROR} name");

            RuleFor(e => e.Email)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .EmailAddress()
                .WithMessage(ResourceErrorMessages.INVALID_EMAIL);

            // TODO: Verificar validador de cpf e cnpj

            RuleFor(e => e.Password)
                .MinimumLength(8)
                .WithMessage(ResourceErrorMessages.PASSWORD_MIN_LENGTH_ERROR);

            RuleFor(e => e.Password)
                .MaximumLength(20)
                .WithMessage(ResourceErrorMessages.PASSWORD_MAX_LENGTH_ERROR);

            RuleFor(request => request)
                .Must(request => request.Password == request.PasswordConfirmation)
                .WithMessage(ResourceErrorMessages.PASSWORD_CONFIRMATION_ERROR);

            RuleFor(e => e.UserType)
                .IsInEnum()
                .WithMessage($"{ResourceErrorMessages.INVALID_ENUM_VALUE} LegalPerson, NaturalPerson");
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

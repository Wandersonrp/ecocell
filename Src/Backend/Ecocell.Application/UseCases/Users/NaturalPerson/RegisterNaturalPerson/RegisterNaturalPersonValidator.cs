using Ecocell.Communication.Requests.Users.NaturalPerson;
using Ecocell.Exception;
using FluentValidation;

namespace Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;

public class RegisterNaturalPersonValidator : AbstractValidator<RequestRegisterNaturalPersonJson>
{
    public RegisterNaturalPersonValidator()
    {
        RuleFor(e => e.Name).NotNull().WithMessage($"{ResourceErrorMessages.NOT_NULL_ERROR} name");
        RuleFor(e => e.Email).EmailAddress().WithMessage(ResourceErrorMessages.INVALID_EMAIL);        
        RuleFor(e => e.Password).MinimumLength(8).WithMessage(ResourceErrorMessages.PASSWORD_MIN_LENGTH_ERROR);
        RuleFor(e => e.Password).MaximumLength(20).WithMessage(ResourceErrorMessages.PASSWORD_MAX_LENGTH_ERROR);
        RuleFor(request => request).Must(request => request.Password == request.PasswordConfirmation).WithMessage(ResourceErrorMessages.PASSWORD_CONFIRMATION_ERROR);
    }
}

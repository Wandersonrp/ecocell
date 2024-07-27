using Ecocell.Communication.Requests.Users.LegalPerson;
using FluentValidation;

namespace Ecocell.Application.UseCases.Users.LegalPerson.RegisterLegalPerson;

public class RegisterLegalPersonValidator : AbstractValidator<RequestRegisterLegalPersonJson>
{
    public RegisterLegalPersonValidator()
    {
        
    }
}

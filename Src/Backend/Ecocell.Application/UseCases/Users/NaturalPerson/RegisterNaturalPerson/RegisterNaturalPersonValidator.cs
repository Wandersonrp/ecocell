using Ecocell.Communication.Requests.Users.NaturalPerson;
using FluentValidation;

namespace Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;

public class RegisterNaturalPersonValidator : AbstractValidator<RequestRegisterNaturalPersonJson>
{
    public RegisterNaturalPersonValidator()
    {
        
    }
}

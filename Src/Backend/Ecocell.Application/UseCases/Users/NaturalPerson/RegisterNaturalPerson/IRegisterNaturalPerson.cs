using Ecocell.Communication.Requests.Users.NaturalPerson;

namespace Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;

public interface IRegisterNaturalPerson
{
    Task Execute(RequestRegisterNaturalPersonJson request);
}

using Ecocell.Communication.Requests.Users.LegalPerson;

namespace Ecocell.Application.UseCases.Users.LegalPerson.RegisterLegalPerson;

public interface IRegisterLegalPerson
{
    Task Execute(RequestRegisterLegalPersonJson request);
}

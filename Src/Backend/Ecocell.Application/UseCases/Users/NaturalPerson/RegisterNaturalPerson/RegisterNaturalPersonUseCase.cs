using Ecocell.Communication.Requests.Users.NaturalPerson;
using Ecocell.Domain.Repositories.Users.NaturalPerson;

namespace Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;

public class RegisterNaturalPersonUseCase : IRegisterNaturalPerson
{
    private readonly INaturalPersonReadOnlyRepository _readOnlyRepository;

    public RegisterNaturalPersonUseCase(INaturalPersonReadOnlyRepository readOnlyRepository)
    {
        _readOnlyRepository = readOnlyRepository;
    }

    public Task Execute(RequestRegisterNaturalPersonJson request)
    {
        Validate(request);

        throw new NotImplementedException();
    }

    private void Validate(RequestRegisterNaturalPersonJson request)
    {
        throw new NotImplementedException();
    }
}

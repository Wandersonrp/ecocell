using Ecocell.Communication.Requests.Users;

namespace Ecocell.Razor.Services.Users.Register;

public interface IRegisterUserService
{
    Task RegisterUser(RequestRegisterUserJson request);
}

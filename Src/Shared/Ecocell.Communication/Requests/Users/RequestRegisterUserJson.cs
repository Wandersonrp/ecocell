using Ecocell.Communication.Requests.Users.NaturalPerson;

namespace Ecocell.Communication.Requests.Users;

public record RequestRegisterUserJson
{
    public RequestRegisterNaturalPersonJson? NaturalPerson { get; set; }
}

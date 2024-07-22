using Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;
using Ecocell.Communication.Requests.Users;
using Microsoft.AspNetCore.Mvc;

namespace Ecocell.API.Controllers.Users.Register;

[Route("api/[controller]")]
[ApiController]
public class RegisterController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Register(
        [FromBody] RequestRegisterUserJson request, 
        [FromServices] IRegisterNaturalPerson naturalPersonUseCase)
    {
        return Created();
    }
}

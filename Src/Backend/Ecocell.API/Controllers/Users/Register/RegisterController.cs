using Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using Ecocell.Communication.Requests.Users.NaturalPerson;
using Microsoft.AspNetCore.Mvc;

namespace Ecocell.API.Controllers.Users.Register;

[Route("api/[controller]")]
[ApiController]
public class RegisterController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Register(
        [FromBody] RequestRegisterUserJson request, 
        [FromServices] IRegisterNaturalPerson naturalPersonUseCase)
    {
        if(request.UserType == UserType.NaturalPerson)
        {
            await naturalPersonUseCase.Execute(new RequestRegisterNaturalPersonJson
            { 
                Name = request.Name,
                Email = request.Email,
                Password = request.Password,
                PasswordConfirmation = request.PasswordConfirmation,
                Document = new RequestDocumentJson
                { 
                    Text = request.Document.Text,
                    DocumentType = request.Document.DocumentType,
                },
                IsDiscarding = request.IsDiscarding
            });
        }

        return CreatedAtAction(nameof(Register), string.Empty);
    }
}

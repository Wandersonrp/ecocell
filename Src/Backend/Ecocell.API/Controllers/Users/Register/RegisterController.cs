using Ecocell.Application.UseCases.Users.LegalPerson.RegisterLegalPerson;
using Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Address;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using Ecocell.Communication.Requests.Users.LegalPerson;
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
        [FromServices] IRegisterNaturalPerson naturalPersonUseCase, 
        [FromServices] IRegisterLegalPerson legalPersonUseCase)
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
        else if(request.UserType == UserType.LegalPerson)
        {
            await legalPersonUseCase.Execute(new RequestRegisterLegalPersonJson
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
                IsDiscarding = request.IsDiscarding,
                IsCollector = request.IsCollector,
                IsDiscardingPoint = request.IsDiscardingPoint,
                Phone = request.Phone,
                CorporateName = request.CorporateName,
                CompanyDescription = request.CompanyDescription,
                CompanyStartDate = request.CompanyStartDate,
                CompanyHierarchy = request.CompanyHierarchy,
                CompanyStatus = request.CompanyStatus,
                PrincipalCnae = request.PrincipalCnae,    
                Address = new RequestAddressJson
                {
                    City = request.Address.City,
                    Complement = request.Address.Complement,
                    Country = request.Address.Country,
                    Neighborhood = request.Address.Neighborhood,
                    Number = request.Address.Number,
                    State = request.Address.State,
                    Street = request.Address.Street,
                    ZipCode = request.Address.ZipCode,
                    Latitude = request.Address.Latitude,
                    Longitude = request.Address.Longitude
                }
            }); ;
        }

        return CreatedAtAction(nameof(Register), string.Empty);
    }
}

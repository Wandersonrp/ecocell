using AutoMapper;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users.NaturalPerson;
using Ecocell.Domain.Entities;

namespace Ecocell.Application.Services.AutoMapper;

public class AutoMapping : Profile
{
    public AutoMapping()
    {
        RequestToDomain();
    }

    private void RequestToDomain()
    {
        CreateMap<RequestDocumentJson, Document>();

        CreateMap<RequestRegisterNaturalPersonJson, NaturalPerson>()
            .ForMember(dest => dest.Password, opt => opt.Ignore())
            .ForMember(dest => dest.BirthDate, opt => opt.Ignore());
    }
}

using AutoMapper;
using Ecocell.Communication.Requests.Users;
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
        CreateMap<RequestRegisterUserJson, NaturalPerson>()
            .ForMember(dest => dest.Password, opt => opt.Ignore());
    }
}

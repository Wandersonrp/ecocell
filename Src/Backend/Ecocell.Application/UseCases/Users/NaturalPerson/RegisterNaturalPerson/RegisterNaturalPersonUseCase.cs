using AutoMapper;
using Ecocell.Communication.Requests.Users.NaturalPerson;
using Ecocell.Domain.Repositories;
using Ecocell.Domain.Repositories.Users.NaturalPerson;
using Ecocell.Domain.Services.Cryptography;
using Ecocell.Exception.ExceptionBase;

namespace Ecocell.Application.UseCases.Users.NaturalPerson.RegisterNaturalPerson;

public class RegisterNaturalPersonUseCase : IRegisterNaturalPerson
{
    private readonly INaturalPersonReadOnlyRepository _naturalPersonReadOnlyRepository;
    private readonly INaturalPersonWriteOnlyRepository _naturalPersonWriteOnlyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IHashGenerator _hashGenerator;

    public RegisterNaturalPersonUseCase(
        INaturalPersonReadOnlyRepository naturalPersonReadOnlyRepository,
        INaturalPersonWriteOnlyRepository naturalPersonWriteOnlyRepository,
        IUnitOfWork unitOfWork, 
        IMapper mapper,
        IHashGenerator hashGenerator
    )
    {
        _naturalPersonReadOnlyRepository = naturalPersonReadOnlyRepository;
        _naturalPersonWriteOnlyRepository = naturalPersonWriteOnlyRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _hashGenerator = hashGenerator;
    }

    public async Task Execute(RequestRegisterNaturalPersonJson request)
    {
        await Validate(request);

        var naturalPerson = _mapper.Map<Domain.Entities.NaturalPerson>(request);

        var hashedPassword = _hashGenerator.Hash(request.Password); 

        naturalPerson.Password = hashedPassword;

        naturalPerson.DocumentId = naturalPerson.Document.DocumentId;

        await _naturalPersonWriteOnlyRepository.AddAsync(naturalPerson);

        await _unitOfWork.CommitAsync();
    }

    private async Task Validate(RequestRegisterNaturalPersonJson request)
    {
        var validation = new RegisterNaturalPersonValidator();

        var result = validation.Validate(request);

        if(!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage).ToList();

            throw new ErrorOnValidationException(errors);
        }

        var userExistsWithSameEmail = await _naturalPersonReadOnlyRepository
            .ExistsWithSameEmail(request.Email);

        if(userExistsWithSameEmail)
        {
            throw new ConflictException(request.Email);
        }

        var userExistsWithSameDocument = await _naturalPersonReadOnlyRepository
            .ExistsWithSameDocument(request.Document.Text);

        if(userExistsWithSameDocument)
        {
            throw new ConflictException(request.Document.Text);
        }
    }
}

using AutoMapper;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Users.LegalPerson;
using Ecocell.Communication.Responses.BrazilApiCompanyData;
using Ecocell.Communication.Responses.Nominatim;
using Ecocell.Domain.Repositories;
using Ecocell.Domain.Repositories.Users.LegalPerson;
using Ecocell.Domain.Services.Geocoding;
using Ecocell.Domain.Services.LegalPersonChecker;
using Ecocell.Exception;
using Ecocell.Exception.ExceptionBase;
using Ecocell.Infrastructure.Utils.Mappers;

namespace Ecocell.Application.UseCases.Users.LegalPerson.RegisterLegalPerson;

public class RegisterLegalPersonUseCase : IRegisterLegalPerson
{
    private readonly ILegalPersonChecker<ResponseBrazilApiCompanyDataJson> _legalPersonChecker;
    private readonly ILegalPersonReadOnlyRepository _legalPersonReadOnlyRepository;
    private readonly ILegalPersonWriteOnlyRepository _legalPersonWriteOnlyRepository;
    private readonly IGeocoding<ResponseNominatimGeocodingJson> _geocoding;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterLegalPersonUseCase(
        ILegalPersonChecker<ResponseBrazilApiCompanyDataJson> legalPersonChecker,
        ILegalPersonReadOnlyRepository legalPersonReadOnlyRepository,
        ILegalPersonWriteOnlyRepository legalPersonWriteOnlyRepository,
        IGeocoding<ResponseNominatimGeocodingJson> geocoding, 
        IMapper mapper, IUnitOfWork unitOfWork)
    {
        _legalPersonChecker = legalPersonChecker;
        _legalPersonReadOnlyRepository = legalPersonReadOnlyRepository;
        _legalPersonWriteOnlyRepository = legalPersonWriteOnlyRepository;
        _geocoding = geocoding;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task Execute(RequestRegisterLegalPersonJson request)
    {
        var geocoding = await Validate(request);

        var legalPerson = _mapper.Map<Domain.Entities.LegalPerson>(request);

        legalPerson.Address.Latitude = geocoding.latitude;
        legalPerson.Address.Longitude = geocoding.longitude;

        await _legalPersonWriteOnlyRepository.AddAsync(legalPerson);

        await _unitOfWork.CommitAsync();
    }

    private async Task<(string latitude, string longitude)> Validate(RequestRegisterLegalPersonJson request)
    {
        var validator = new RegisterLegalPersonValidator();
        
        var result = validator.Validate(request);

        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage).ToList();

            throw new ErrorOnValidationException(errors);
        }
        
        var userExistsWithSameDocument = await _legalPersonReadOnlyRepository.ExistsWithSameDocument(request.Document.Text);

        if(userExistsWithSameDocument)
        {
            throw new ConflictException(request.Document.Text);
        }

        var userExistsWithSameEmail = await _legalPersonReadOnlyRepository.ExistsWithSameEmail(request.Email);

        if (userExistsWithSameEmail)
        {
            throw new ConflictException(request.Email);
        }
        
        // verify if the company registration is active in the federal revenue, if it is not active, do not register
        var companyData = await _legalPersonChecker.GetCompanyData(request.Document.Text) ??
            throw new NotFoundException(request.Document.Text);        

        if(MapperCompanyStatus.ToCompanyStatus(companyData.CompanyStatusDescription) is not CompanyStatus.Active)
        {
            throw new ErrorOnValidationException([ResourceErrorMessages.COMPANY_IS_NOT_ACTIVE_ERROR]);
        }

        return await GetGeocoding(companyData.ZipCode.ToString());
    }

    // returns the latitude and longitude of the postal code
    // TODO: talvez criar um use case para o usuario buscar a latitude e longitude de um cep
    private async Task<(string latitude, string longitude)> GetGeocoding(string postalCode)
    {
        var geocodingResult = await _geocoding.GetGeocodingByPostalCode(postalCode);

        if (geocodingResult is null)
        {
            throw new NotFoundException(postalCode);
        }

        var latitude = geocodingResult.Roots.FirstOrDefault()?.Latitude ?? throw new NotFoundException("latitude");

        var longitude = geocodingResult.Roots.FirstOrDefault()?.Longitude ?? throw new NotFoundException("longitude");

        (string latitude, string longitude) tuple = (latitude, longitude);

        return tuple;
    }
}

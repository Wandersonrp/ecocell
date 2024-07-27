using Ecocell.Communication.Responses.BrazilApiCompanyData;
using Ecocell.Domain.Services.LegalPersonChecker;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Ecocell.Infrastructure.Services.LegalPersonChecker;

public class BrazilApiCompanyChecker : ILegalPersonChecker<ResponseBrazilApiCompanyDataJson>
{    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BrazilApiCompanyChecker(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<ResponseBrazilApiCompanyDataJson?> GetCompanyData(string cnpj)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        var url = _configuration.GetValue<string>("BrasilApiUrl");

        try
        {            
            var response = await httpClient.GetFromJsonAsync<ResponseBrazilApiCompanyDataJson>($"/cnpj/v1/{cnpj}");    
            
            if(response is null)
            {
                return null;
            }            

            return response;
        }
        catch
        {
            throw;
        }        
    }    
}

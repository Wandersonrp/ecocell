using Ecocell.Communication.Responses.BrazilApiCompanyData;
using System.Net.Http.Json;

namespace Ecocell.Razor.Services.LegalPersonChecker;

public class BrazilApiCompanyChecker : ILegalPersonChecker<ResponseBrazilApiCompanyDataJson>
{    
    public async Task<ResponseBrazilApiCompanyDataJson?> GetCompanyData(string cnpj)
    {
        var handler = new ModernHttpClient.NativeMessageHandler();
        using var httpClient = new HttpClient(handler);

        var url = $"https://brasilapi.com.br/api/cnpj/v1/{cnpj}";

        try
        {            
            var response = await httpClient.GetFromJsonAsync<ResponseBrazilApiCompanyDataJson>(url);    
            
            if(response is null)
            {
                return null;
            }            

            return response;
        }
        catch (System.Exception ex)
        {
            return null;
        }        
    }    
}

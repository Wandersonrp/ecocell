using Ecocell.Communication.Requests.Users;
using System.Net.Http.Json;

namespace Ecocell.Razor.Services.Users.Register;

public class RegisterUserService : IRegisterUserService
{
    private readonly HttpClient _httpClient;

    public RegisterUserService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task RegisterUser(RequestRegisterUserJson request)
    {
        try
        {
            var handler = new ModernHttpClient.NativeMessageHandler();
            using var httpClient = new HttpClient(handler);

            var url = "http://localhost:5150";

            var response = await _httpClient.PostAsJsonAsync($"{url}/api/register", request);

            if(!response.IsSuccessStatusCode)
            {
                // TODO verificar retorno do backend
                await Console.Out.WriteLineAsync(response.StatusCode.ToString());
            }
        }
        catch(System.Exception ex)
        {
            await Console.Out.WriteLineAsync(ex.Message); 
        }        
    }
}

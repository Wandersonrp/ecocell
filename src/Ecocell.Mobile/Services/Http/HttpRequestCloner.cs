namespace Ecocell.Mobile.Services.Http;

/// <summary>
/// Clona um <see cref="HttpRequestMessage"/> para reenvio — uma request já enviada
/// não pode ser reutilizada pelo HttpClient. Usado pelo retry de falha transitória
/// e pelo retry pós-refresh de token.
/// </summary>
public static class HttpRequestCloner
{
    public static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Version = request.Version;
        return clone;
    }
}

namespace Ecocell.Mobile.Services.Http;

/// <summary>
/// Repete uma única vez requests que falham com <see cref="HttpRequestException"/>
/// (falha de transporte, antes de qualquer resposta HTTP). No Android, a primeira
/// conexão HTTPS do processo pode falhar durante o warm-up do provedor de segurança
/// do Google Play Services (DEVELOPER_ERROR no logcat); a repetição imediata
/// funciona. Anexado a todos os clients Refit em MauiProgram.
/// </summary>
public sealed class TransientRetryHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            var retry = await HttpRequestCloner.CloneAsync(request);
            return await base.SendAsync(retry, cancellationToken);
        }
    }
}

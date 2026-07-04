using Refit;
using Ecocell.Shared.Requests;

namespace Ecocell.Mobile.Services.Api;

public interface ILegalPersonClient
{
    [Post("/api/legal-person")]
    Task<IApiResponse> RegisterAsync(
        [Body] RequestRegisterLegalPerson request,
        CancellationToken ct = default);
}

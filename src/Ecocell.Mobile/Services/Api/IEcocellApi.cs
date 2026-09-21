using Refit;
using Ecocell.Shared.Requests;

namespace Ecocell.Mobile.Services.Api;

public interface IEcocellApi
{
    [Post("/api/natural-person")]
    Task<IApiResponse> RegisterNaturalPersonAsync(
        [Body] RequestRegisterNaturalPerson request,
        CancellationToken ct = default);
}

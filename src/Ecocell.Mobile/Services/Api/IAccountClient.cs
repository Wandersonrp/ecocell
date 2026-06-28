using Refit;
using Ecocell.Shared.Requests;

namespace Ecocell.Mobile.Services.Api;

public interface IAccountClient
{
    [Post("/api/account/confirm")]
    Task<IApiResponse> ConfirmAccountAsync(
        [Body] RequestConfirmAccount request,
        CancellationToken ct = default);

    [Post("/api/account/resend-code")]
    Task<IApiResponse> ResendVerificationCodeAsync(
        [Body] RequestResendVerificationCode request,
        CancellationToken ct = default);
}

using Refit;
using Ecocell.Shared.Requests;
using Ecocell.Shared.Responses;

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

    [Post("/api/account/login/request-code")]
    Task<IApiResponse> RequestLoginCodeAsync(
        [Body] RequestRequestLoginCode request,
        CancellationToken ct = default);

    [Post("/api/account/login")]
    Task<IApiResponse<ResponseLogin>> VerifyLoginCodeAsync(
        [Body] RequestVerifyLoginCode request,
        CancellationToken ct = default);
}

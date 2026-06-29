using Ecocell.Shared.Responses;

namespace Ecocell.Mobile.Services.Auth;

public interface ISecureTokenStore
{
    Task SaveAsync(ResponseLogin tokens);
    Task<StoredTokens?> GetAsync();
    Task ClearAsync();
}

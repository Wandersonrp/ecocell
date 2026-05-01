using StackExchange.Redis;

namespace Ecocell.Api.Services.Authentication;

/// <summary>
/// Implementação de <see cref="IRefreshTokenStore"/> com Redis STRING.
/// Chave: <c>refresh:{token}</c> → valor: personId (GUID string), TTL nativo Redis.
/// </summary>
public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    private readonly IDatabase _db;

    public RedisRefreshTokenStore(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task SaveAsync(string token, Guid personId, TimeSpan ttl, CancellationToken ct = default)
        => await _db.StringSetAsync(BuildKey(token), personId.ToString(), ttl);

    public async Task<Guid?> GetPersonIdAsync(string token, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(BuildKey(token));
        if (!value.HasValue) return null;
        return Guid.TryParse(value.ToString(), out var id) ? id : null;
    }

    public async Task DeleteAsync(string token, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(BuildKey(token));

    private static string BuildKey(string token) => $"refresh:{token}";
}

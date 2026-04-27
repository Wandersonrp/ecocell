using StackExchange.Redis;

namespace Ecocell.Api.Services.VerificationCodes;

/// <summary>
/// Implementação de <see cref="IVerificationCodeStore"/> com armazenamento no Redis.
/// Cada registro é um HASH com os campos <c>code</c>, <c>attempts</c> e <c>issuedAt</c>,
/// com TTL gerenciado nativamente pela chave.
/// </summary>
public sealed class RedisVerificationCodeStore : IVerificationCodeStore
{
    private readonly IDatabase _db;

    public RedisVerificationCodeStore(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    /// <summary>
    /// Persiste o hash do código OTP no Redis com o TTL e contador de tentativas zerado.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="codeHash">Hash SHA-256 do código gerado.</param>
    /// <param name="ttl">Tempo de vida do registro.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task SaveAsync(string key, string codeHash, TimeSpan ttl, CancellationToken ct = default)
    {
        var entries = new HashEntry[]
        {
            new("code", codeHash),
            new("attempts", 0),
            new("issuedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _db.HashSetAsync(key, entries);
        await _db.KeyExpireAsync(key, ttl);
    }

    /// <summary>
    /// Recupera o registro OTP associado à chave; retorna <c>null</c> se inexistente ou expirado.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<VerificationCodeRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        var entries = await _db.HashGetAllAsync(key);
        if (entries.Length == 0)
            return null;

        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        if (!dict.TryGetValue("code", out var codeHash) ||
            !dict.TryGetValue("attempts", out var attemptsStr) ||
            !dict.TryGetValue("issuedAt", out var issuedAtStr))
            return null;

        var attempts = int.TryParse(attemptsStr, out var a) ? a : 0;
        var issuedAt = long.TryParse(issuedAtStr, out var ts)
            ? DateTimeOffset.FromUnixTimeSeconds(ts)
            : DateTimeOffset.UtcNow;

        return new VerificationCodeRecord(codeHash, attempts, issuedAt);
    }

    /// <summary>
    /// Incrementa o contador de tentativas inválidas para a chave informada.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task IncrementAttemptsAsync(string key, CancellationToken ct = default) =>
        await _db.HashIncrementAsync(key, "attempts");

    /// <summary>
    /// Remove o registro OTP da chave informada após confirmação bem-sucedida.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await _db.KeyDeleteAsync(key);
}

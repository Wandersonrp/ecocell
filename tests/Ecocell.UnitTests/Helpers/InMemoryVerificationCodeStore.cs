using System.Collections.Concurrent;
using Ecocell.Api.Services.VerificationCodes;

namespace Ecocell.UnitTests.Helpers;

/// <summary>
/// Implementação de <see cref="IVerificationCodeStore"/> baseada em memória para uso exclusivo nos testes de unidade.
/// Substitui o Redis sem depender de infraestrutura externa.
/// </summary>
public sealed class InMemoryVerificationCodeStore : IVerificationCodeStore
{
    private readonly ConcurrentDictionary<string, Entry> _store = new();

    private sealed record Entry(string CodeHash, int Attempts, DateTimeOffset IssuedAt, DateTimeOffset Expires);

    /// <summary>
    /// Pré-popula o store com um registro para uso em cenários de teste.
    /// </summary>
    /// <param name="key">Chave de identificação (use <see cref="IVerificationCodeStore.BuildKey"/>).</param>
    /// <param name="codeHash">Hash SHA-256 do código a ser armazenado.</param>
    /// <param name="expires">Momento de expiração do registro (use valor no passado para simular código expirado).</param>
    /// <param name="attempts">Número de tentativas inválidas já registradas (padrão: 0).</param>
    public void Seed(string key, string codeHash, DateTimeOffset expires, int attempts = 0)
    {
        _store[key] = new Entry(codeHash, attempts, DateTimeOffset.UtcNow, expires);
    }

    /// <summary>
    /// Persiste o hash do código OTP com TTL e contador de tentativas zerado.
    /// </summary>
    public Task SaveAsync(string key, string codeHash, TimeSpan ttl, CancellationToken ct = default)
    {
        _store[key] = new Entry(codeHash, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Recupera o registro OTP associado à chave; retorna <c>null</c> se inexistente ou expirado.
    /// </summary>
    public Task<VerificationCodeRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(key, out var entry) || entry.Expires <= DateTimeOffset.UtcNow)
        {
            _store.TryRemove(key, out _);
            return Task.FromResult<VerificationCodeRecord?>(null);
        }

        var record = new VerificationCodeRecord(entry.CodeHash, entry.Attempts, entry.IssuedAt);
        return Task.FromResult<VerificationCodeRecord?>(record);
    }

    /// <summary>
    /// Incrementa o contador de tentativas inválidas para a chave informada.
    /// </summary>
    public Task IncrementAttemptsAsync(string key, CancellationToken ct = default)
    {
        _store.AddOrUpdate(
            key,
            _ => new Entry(string.Empty, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            (_, existing) => existing with { Attempts = existing.Attempts + 1 });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Remove o registro OTP da chave informada.
    /// </summary>
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retorna o número de tentativas registradas para a chave; útil para asserções nos testes.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    public int GetAttempts(string key) =>
        _store.TryGetValue(key, out var entry) ? entry.Attempts : 0;

    /// <summary>
    /// Retorna <c>true</c> se existe um registro ativo (não expirado) para a chave informada.
    /// </summary>
    /// <param name="key">Chave de identificação no store.</param>
    public bool HasActiveEntry(string key) =>
        _store.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow;
}

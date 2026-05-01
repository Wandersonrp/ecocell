namespace Ecocell.Api.Services.Authentication;

/// <summary>
/// Contrato para armazenamento e recuperação de refresh tokens opacos com TTL automático via Redis.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Persiste o token com o identificador da pessoa e TTL.
    /// Redis key: <c>refresh:{token}</c> → valor: personId.ToString().
    /// </summary>
    Task SaveAsync(string token, Guid personId, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Recupera o personId associado ao token; retorna <c>null</c> se inexistente ou expirado.
    /// </summary>
    Task<Guid?> GetPersonIdAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Remove o token do store. Chamar ANTES de emitir novo par (one-time-use rotation).
    /// </summary>
    Task DeleteAsync(string token, CancellationToken ct = default);
}

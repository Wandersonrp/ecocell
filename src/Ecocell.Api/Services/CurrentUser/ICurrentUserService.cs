namespace Ecocell.Api.Services.CurrentUser;

/// <summary>
/// Contrato para obtenção do usuário autenticado na requisição corrente.
/// Retorna <c>null</c> quando não há usuário autenticado ou o Id do claim é inválido.
/// </summary>
public interface ICurrentUserService
{
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default);
}
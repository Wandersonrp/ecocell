namespace Ecocell.Api.Shared;

/// <summary>Constantes com os nomes das policies de autorização registradas na API.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Policy que exige um usuário autenticado via JWT válido.</summary>
    public const string Authenticated = "Authenticated";
}

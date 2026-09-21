using Ecocell.Shared.Enums;

namespace Ecocell.Mobile.Services.Navigation;

/// <summary>
/// Resolve a rota inicial após uma sessão autenticada ser confirmada — tanto na
/// verificação de OTP quanto no redirecionamento automático de sessão persistida
/// no boot do app. Admin vai à área de solicitações; Depositante ao mapa; demais
/// jornadas ainda não têm tela dedicada e caem na tela inicial.
/// </summary>
public static class PostLoginRouteResolver
{
    public static string Resolve(Role? role, Journey? journey)
    {
        if (role == Role.Admin)
        {
            return "/admin/solicitacoes";
        }

        return journey == Journey.Depositor ? "/map" : "/";
    }
}

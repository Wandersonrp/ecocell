namespace Ecocell.Api.Enums;

/// <summary>Papel do usuário na plataforma EcoCell (uso interno da API).</summary>
public enum Role
{
    /// <summary>Administrador da plataforma.</summary>
    Admin = 1,
    /// <summary>Suporte operacional.</summary>
    Support = 2,
    /// <summary>Usuário comum (Depositante, Ponto de Coleta ou Coletor).</summary>
    User = 3,
}

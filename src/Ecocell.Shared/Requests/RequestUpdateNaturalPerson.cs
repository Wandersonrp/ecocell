namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição para atualização do perfil da pessoa física autenticada.
/// </summary>
public record RequestUpdateNaturalPerson
{
    /// <summary>Novo nome completo da pessoa física.</summary>
    public string FullName { get; set; } = string.Empty;
}
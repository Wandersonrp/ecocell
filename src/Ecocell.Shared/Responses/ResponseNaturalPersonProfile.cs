using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses;

/// <summary>
/// Perfil completo da pessoa física autenticada, retornado pelo endpoint GET /api/natural-person/me.
/// </summary>
public sealed record ResponseNaturalPersonProfile
{
    /// <summary>Identificador único da pessoa.</summary>
    public Guid Id { get; init; }

    /// <summary>Nome completo.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Endereço de e-mail.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>CPF com 11 dígitos sem formatação.</summary>
    public string Cpf { get; init; } = string.Empty;

    /// <summary>Data de nascimento.</summary>
    public DateOnly BirthDate { get; init; }

    /// <summary>Papel do usuário na plataforma.</summary>
    public Role Role { get; init; }

    /// <summary>Jornada de descarte selecionada.</summary>
    public Journey Journey { get; init; }

    /// <summary>Tipo de pessoa (física ou jurídica).</summary>
    public PersonType PersonType { get; init; }

    /// <summary>Status atual do cadastro.</summary>
    public PersonStatus PersonStatus { get; init; }

    /// <summary>Data e hora de criação do registro em UTC.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Data e hora da última atualização em UTC, se houver.</summary>
    public DateTime? UpdatedAt { get; init; }
}

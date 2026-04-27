using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

/// <summary>
/// Entidade base abstrata que representa qualquer tipo de pessoa cadastrada na plataforma.
/// </summary>
public abstract class Person : BaseEntity
{
    public Role Role { get; protected set; } = Role.User;
    public string Email { get; protected set; } = string.Empty;
    public Journey Journey { get; protected set; }
    public PersonType PersonType { get; protected set; }
    public PersonStatus PersonStatus { get; protected set; } = PersonStatus.AwaitingConfirmation;

    /// <summary>
    /// Confirma o cadastro da pessoa, transitando o status de <see cref="PersonStatus.AwaitingConfirmation"/>
    /// para <see cref="PersonStatus.Active"/>. Lança exceção se o status atual não permitir a transição.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Lançada quando o status atual não é <see cref="PersonStatus.AwaitingConfirmation"/>.
    /// </exception>
    internal void Confirm()
    {
        if (PersonStatus != PersonStatus.AwaitingConfirmation)
            throw new InvalidOperationException(
                $"Não é possível confirmar uma conta com status '{PersonStatus}'.");

        PersonStatus = PersonStatus.Active;
        MarkAsUpdated();
    }
}

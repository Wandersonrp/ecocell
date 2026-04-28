using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public class NaturalPerson : Person
{
    public string FullName { get; private set; } = string.Empty;
    public string Cpf { get; private set; } = string.Empty;
    public DateOnly BirthDate { get; private set; }
    public virtual ICollection<LegalPerson> ManagedCompanies { get; private set; } = new List<LegalPerson>();

    public NaturalPerson(
        string fullName,
        string cpf,
        DateOnly birthDate,
        Role role,
        string email,
        Journey journey)
    {
        FullName = fullName;
        Cpf = cpf;
        BirthDate = birthDate;
        Role = role;
        Email = email;
        Journey = journey;
        PersonType = PersonType.NaturalPerson;
    }

    /// <summary>
    /// Atualiza os dados editáveis da pessoa física.
    /// </summary>
    /// <param name="fullName">Novo nome completo.</param>
    internal void Update(string fullName)
    {
        FullName = fullName;
        MarkAsUpdated();
    }
}

using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public class LegalPerson : Person
{    
    public string LegalName { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public string Cnpj { get; private set; } = string.Empty;
    public string? Cnae { get; private set; }

    public Guid? ResponsiblePersonId { get; private set; }
    public virtual NaturalPerson? ResponsiblePerson { get; private set; }

    public LegalPerson(
        string legalName, 
        string tradeName, 
        string cnpj,
        string email,
        Journey journey, 
        Guid? responsiblePersonId = null, 
        string? cnae = null)
    {
        LegalName = legalName;
        TradeName = tradeName;
        Email = email;
        Cnpj = cnpj;
        ResponsiblePersonId = responsiblePersonId;
        Role = Role.User;
        Journey = journey;
        PersonType = PersonType.LegalPerson;
        Cnae = cnae;

        if (journey != Journey.Depositor &&
            journey != Journey.None)
            PersonStatus = PersonStatus.AwaitingConfirmation;
    }
}

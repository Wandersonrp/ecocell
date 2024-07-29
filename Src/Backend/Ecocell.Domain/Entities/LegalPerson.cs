using Ecocell.Communication.Enums.User;

namespace Ecocell.Domain.Entities;

public class LegalPerson : User
{
    public string CorporateName { get; set; } = string.Empty;
    public string CompanyDescription { get; set; } = string.Empty;
    public DateOnly CompanyStartDate { get; set; }
    public CompanyHierarchy CompanyHierarchy { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string PrincipalCnae { get; set; } = string.Empty;
    public CompanyStatus CompanyStatus { get; set; }   
    public bool IsDiscarding { get; set; }
    public bool IsCollector { get; set; }
    public bool IsDiscardingPoint { get; set; }
    public Guid AddressId { get; set; }
    public Address Address { get; set; } = null!;
}

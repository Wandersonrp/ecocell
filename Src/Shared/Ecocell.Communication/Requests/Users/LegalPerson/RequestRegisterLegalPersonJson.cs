using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Address;
using Ecocell.Communication.Requests.Document;
using System.Text.Json.Serialization;

namespace Ecocell.Communication.Requests.Users.LegalPerson;

public record RequestRegisterLegalPersonJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("document")]
    public RequestDocumentJson Document { get; set; } = null!;

    [JsonPropertyName("password_confirmation")]
    public string PasswordConfirmation { get; set; } = string.Empty;

    [JsonPropertyName("corporate_name")]
    public string CorporateName { get; set; } = string.Empty;

    [JsonPropertyName("company_description")]
    public string CompanyDescription { get; set; } = string.Empty;

    [JsonPropertyName("company_hierarchy")]
    public CompanyHierarchy CompanyHierarchy { get; set; }

    [JsonPropertyName("company_start_date")]
    public DateOnly CompanyStartDate { get; set; }

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("principal_cnae")]
    public string PrincipalCnae { get; set; } = string.Empty;

    [JsonPropertyName("comapny_status")]
    public CompanyStatus CompanyStatus { get; set; }

    [JsonPropertyName("is_discarding")]
    public bool IsDiscarding { get; set; }

    [JsonPropertyName("is_collector")]
    public bool IsCollector { get; set; }

    [JsonPropertyName("is_discarding_point")]
    public bool IsDiscardingPoint { get; set; }    
    public RequestAddressJson Address { get; set; } = null!;
}

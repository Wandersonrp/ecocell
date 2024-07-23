using Ecocell.Communication.Requests.Document;
using System.Text.Json.Serialization;

namespace Ecocell.Communication.Requests.Users.NaturalPerson;

public record RequestRegisterNaturalPersonJson
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

    [JsonPropertyName("is_discarding")]
    public bool IsDiscarding { get; set; }
}

using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Document;
using System.Text.Json.Serialization;

namespace Ecocell.Communication.Requests.Users;

public record RequestRegisterUserJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("document_text")]
    public RequestDocumentJson Document { get; set; } = null!;

    [JsonPropertyName("password_confirmation")]
    public string PasswordConfirmation { get; set; } = string.Empty;

    [JsonPropertyName("user_type")]
    public UserType UserType { get; set; }

    [JsonPropertyName("is_discarding")]
    public bool IsDiscarding { get; set; }

    [JsonPropertyName("is_collector")]
    public bool IsCollector { get; set; }

    [JsonPropertyName("is_discarding_point")]
    public bool IsDiscardingPoint { get; set; }
}

using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Status de cadastro de uma pessoa na plataforma EcoCell.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PersonStatus
{
    Inactive = 0,
    Active = 1,
    Suspended = 2,
    AwaitingConfirmation = 3,
    PendingApproval = 4,
    Refused = 5,
}

using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Papel do usuário na plataforma EcoCell.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    Admin = 1,
    Support = 2,
    User = 3,
}

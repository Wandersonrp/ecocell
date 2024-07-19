using System.Text.Json.Serialization;

namespace Ecocell.Communication.Enums.User;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    User,
    Admin
}

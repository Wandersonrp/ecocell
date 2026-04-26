using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PersonType
{
    NaturalPerson = 1,
    LegalPerson = 2
}

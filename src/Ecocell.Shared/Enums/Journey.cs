using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Journey
{
    None = 0,
    Depositor = 1,
    CollectPoint = 2,
    Collector = 3,
}
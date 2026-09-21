using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RankingScope
{
    Municipal = 0,
    National = 1
}

using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Unidade usada para conceder pontos por material.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaterialScoreUnit
{
    PerUnit = 1,
    PerKilogram = 2,
}

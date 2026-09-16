using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Status atual de um descarte.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscardStatus
{
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
}

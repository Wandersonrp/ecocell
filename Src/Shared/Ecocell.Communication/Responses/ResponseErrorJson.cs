using System.Text.Json.Serialization;

namespace Ecocell.Communication.Responses;

public record ResponseErrorJson
{
    [JsonPropertyName("errors")]
    public IList<string> Errors { get; set; } = [];

    public ResponseErrorJson(IList<string> errors)
    {
        Errors = errors;
    }
}

using Ecocell.Communication.Enums.Document;
using System.Text.Json.Serialization;

namespace Ecocell.Communication.Requests.Document;

public record RequestDocumentJson
{
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

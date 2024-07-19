using System.Text.Json.Serialization;

namespace Ecocell.Communication.Enums.Document;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentType
{
    CPF,
    CNPJ
}

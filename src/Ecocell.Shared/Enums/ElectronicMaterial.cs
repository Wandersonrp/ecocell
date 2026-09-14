using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Materiais eletrônicos aceitos pelas regras de pontuação.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ElectronicMaterial
{
    Battery = 1,
    CellPhone = 2,
    PortableGameConsole = 3,
    Headphones = 4,
    Printer = 5,
    Notebook = 6,
    SmallAppliance = 7,
}

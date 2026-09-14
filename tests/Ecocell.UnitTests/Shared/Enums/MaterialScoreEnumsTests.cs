using System.Text.Json;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.UnitTests.Shared.Enums;

public class MaterialScoreEnumsTests
{
    [Fact]
    public void ElectronicMaterial_ShouldKeepPublicAndInternalContractsAligned()
    {
        var expected = new (string Name, int Value)[]
        {
            ("Battery", 1),
            ("CellPhone", 2),
            ("PortableGameConsole", 3),
            ("Headphones", 4),
            ("Printer", 5),
            ("Notebook", 6),
            ("SmallAppliance", 7),
        };

        ReadValues<ApiEnums.ElectronicMaterial>().ShouldBe(expected);
        ReadValues<SharedEnums.ElectronicMaterial>().ShouldBe(expected);
    }

    [Fact]
    public void MaterialScoreUnit_ShouldKeepPublicAndInternalContractsAligned()
    {
        var expected = new (string Name, int Value)[]
        {
            ("PerUnit", 1),
            ("PerKilogram", 2),
        };

        ReadValues<ApiEnums.MaterialScoreUnit>().ShouldBe(expected);
        ReadValues<SharedEnums.MaterialScoreUnit>().ShouldBe(expected);
    }

    [Fact]
    public void PublicEnums_ShouldSerializeAsStrings()
    {
        JsonSerializer.Serialize(SharedEnums.ElectronicMaterial.CellPhone)
            .ShouldBe("\"CellPhone\"");
        JsonSerializer.Serialize(SharedEnums.MaterialScoreUnit.PerKilogram)
            .ShouldBe("\"PerKilogram\"");
    }

    private static (string Name, int Value)[] ReadValues<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => (value.ToString(), Convert.ToInt32(value)))
            .ToArray();
}

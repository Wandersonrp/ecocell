using Ecocell.Shared.Enums;

namespace Ecocell.Mobile.Models.Discards;

public static class ElectronicMaterialDisplay
{
    public static string Label(ElectronicMaterial material) => material switch
    {
        ElectronicMaterial.Battery => "Pilhas e baterias",
        ElectronicMaterial.CellPhone => "Celulares",
        ElectronicMaterial.PortableGameConsole => "Consoles portáteis",
        ElectronicMaterial.Headphones => "Fones de ouvido",
        ElectronicMaterial.Printer => "Impressoras",
        ElectronicMaterial.Notebook => "Notebooks",
        ElectronicMaterial.SmallAppliance => "Pequenos eletrodomésticos",
        _ => material.ToString(),
    };
}

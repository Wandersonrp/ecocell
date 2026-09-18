using System.Globalization;
using Ecocell.Shared.Enums;

namespace Ecocell.Mobile.Models.Discards;

public sealed class DiscardItemDraft
{
    public ElectronicMaterial? Material { get; set; }
    public string QuantityText { get; set; } = string.Empty;
    public string WeightText { get; set; } = string.Empty;

    public string? MaterialError { get; private set; }
    public string? QuantityError { get; private set; }
    public string? WeightError { get; private set; }

    public bool TryGetValues(
        bool materialIsDuplicated,
        out ElectronicMaterial material,
        out int quantity,
        out decimal approximateWeightKg)
    {
        MaterialError = Material is null
            ? "Selecione um material."
            : materialIsDuplicated
                ? "Este material já foi adicionado."
                : null;

        material = Material.GetValueOrDefault();
        quantity = 0;
        approximateWeightKg = 0m;

        if (!int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity)
            || quantity <= 0)
        {
            QuantityError = "Informe uma quantidade inteira maior que zero.";
        }
        else
        {
            QuantityError = null;
        }

        var normalizedWeight = WeightText.Trim().Replace(',', '.');
        var hasValidWeight = decimal.TryParse(
            normalizedWeight,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out approximateWeightKg);
        var decimalPlaces = normalizedWeight.Contains('.')
            ? normalizedWeight.Length - normalizedWeight.IndexOf('.') - 1
            : 0;

        if (!hasValidWeight || approximateWeightKg <= 0m || decimalPlaces > 3 || approximateWeightKg > 9999999.999m)
        {
            WeightError = "Informe um peso maior que zero, com no máximo três casas decimais.";
        }
        else
        {
            WeightError = null;
        }

        return MaterialError is null && QuantityError is null && WeightError is null;
    }
}

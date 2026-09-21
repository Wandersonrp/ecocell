namespace Ecocell.Shared.Utils;

public static class DocumentFormatter
{
    public static string FormatCnpj(string cnpj)
    {
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        return digits.Length == 14
            ? $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..14]}"
            : cnpj;
    }
}

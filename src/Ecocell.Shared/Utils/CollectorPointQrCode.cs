namespace Ecocell.Shared.Utils;

public static class CollectorPointQrCode
{
    private const string Prefix = "ecocell://pc/";

    public static bool TryParse(string? value, out Guid collectorPointId)
    {
        collectorPointId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rawId = value[Prefix.Length..];
        if (rawId.Length != 36
            || !Guid.TryParseExact(rawId, "D", out var parsedId)
            || rawId != parsedId.ToString("D"))
        {
            return false;
        }

        collectorPointId = parsedId;
        return true;
    }
}

namespace Ecocell.Shared.Utils;

public static class DocumentValidator
{
    public static bool IsCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;

        Span<int> digits = stackalloc int[11];
        if (!TryExtractDigits(cpf, digits, out var count) || count != 11) return false;
        if (AllEqual(digits)) return false;

        ReadOnlySpan<int> w1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        if (digits[9] != ComputeCheckDigit(digits[..9], w1)) return false;

        ReadOnlySpan<int> w2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
        return digits[10] == ComputeCheckDigit(digits[..10], w2);
    }

    public static bool IsCnpj(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj)) return false;

        Span<int> digits = stackalloc int[14];
        if (!TryExtractDigits(cnpj, digits, out var count) || count != 14) return false;
        if (AllEqual(digits)) return false;
        if (AllEqual(digits[..8])) return false;

        ReadOnlySpan<int> w1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        if (digits[12] != ComputeCheckDigit(digits[..12], w1)) return false;

        ReadOnlySpan<int> w2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return digits[13] == ComputeCheckDigit(digits[..13], w2);
    }

    private static bool TryExtractDigits(string input, Span<int> buffer, out int count)
    {
        count = 0;
        foreach (var c in input)
        {
            if (!char.IsDigit(c)) continue;
            if (count >= buffer.Length) return false;
            buffer[count++] = c - '0';
        }
        return true;
    }

    private static bool AllEqual(ReadOnlySpan<int> span)
    {
        for (var i = 1; i < span.Length; i++)
            if (span[i] != span[0]) return false;
        return true;
    }

    private static int ComputeCheckDigit(ReadOnlySpan<int> digits, ReadOnlySpan<int> weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += digits[i] * weights[i];
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}

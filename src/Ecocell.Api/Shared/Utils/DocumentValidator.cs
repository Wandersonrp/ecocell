namespace Ecocell.Api.Shared.Utils;

public static class DocumentValidator
{
    public static bool IsCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;

        var position = 0;
        Span<int> digits = stackalloc int[11];

        foreach (var c in cpf)
        {
            if (char.IsDigit(c))
            {
                if (position >= 11) return false;
                digits[position++] = c - '0';
            }
        }

        if (position != 11) return false;

        for (var i = 1; i < 11; i++)
            if (digits[i] != digits[0]) goto checkDigits;

        return false;

        checkDigits:
        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += digits[i] * (10 - i);

        var remainder = sum % 11;
        if (digits[9] != (remainder < 2 ? 0 : 11 - remainder)) return false;

        sum = 0;
        for (var i = 0; i < 10; i++)
            sum += digits[i] * (11 - i);

        remainder = sum % 11;
        return digits[10] == (remainder < 2 ? 0 : 11 - remainder);
    }

    public static bool IsCnpj(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj)) return false;

        var position = 0;
        Span<int> digits = stackalloc int[14];

        foreach (var c in cnpj)
        {
            if (char.IsDigit(c))
            {
                if (position >= 14) return false;
                digits[position++] = c - '0';
            }
        }

        if (position != 14) return false;

        // Rejeita CNPJs onde todos os 14 dígitos são iguais (ex: 00000000000000).
        var allSame = true;
        for (var i = 1; i < 14; i++)
            if (digits[i] != digits[0]) { allSame = false; break; }

        if (allSame) return false;

        // Rejeita CNPJs onde os 8 primeiros dígitos (base) são todos iguais,
        // por exemplo 11111111/0001-XX é inválido pela Receita Federal.
        var allBaseEqual = true;
        for (var i = 1; i < 8; i++)
            if (digits[i] != digits[0]) { allBaseEqual = false; break; }

        if (allBaseEqual) return false;

        ReadOnlySpan<int> weights1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += digits[i] * weights1[i];

        var remainder = sum % 11;
        if (digits[12] != (remainder < 2 ? 0 : 11 - remainder)) return false;

        ReadOnlySpan<int> weights2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        sum = 0;
        for (var i = 0; i < 13; i++)
            sum += digits[i] * weights2[i];

        remainder = sum % 11;
        return digits[13] == (remainder < 2 ? 0 : 11 - remainder);
    }
}

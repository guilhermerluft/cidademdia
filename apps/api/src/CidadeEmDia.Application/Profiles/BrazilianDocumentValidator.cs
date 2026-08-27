namespace CidadeEmDia.Application.Profiles;

public static class BrazilianDocumentValidator
{
    public static bool TryNormalize(string? value, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        normalized = new string(value.Where(char.IsDigit).ToArray());
        if (normalized.Length is not (11 or 14))
            return false;

        if (normalized.Distinct().Count() == 1)
            return false;

        return normalized.Length == 11
            ? IsValidCpf(normalized)
            : IsValidCnpj(normalized);
    }

    private static bool IsValidCpf(string cpf)
    {
        var first = CalculateDigit(cpf, 9, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        if (cpf[9] - '0' != first)
            return false;

        var second = CalculateDigit(cpf, 10, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return cpf[10] - '0' == second;
    }

    private static bool IsValidCnpj(string cnpj)
    {
        var first = CalculateDigit(cnpj, 12, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        if (cnpj[12] - '0' != first)
            return false;

        var second = CalculateDigit(cnpj, 13, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return cnpj[13] - '0' == second;
    }

    private static int CalculateDigit(string value, int length, IReadOnlyList<int> weights)
    {
        var sum = 0;
        for (var index = 0; index < length; index++)
            sum += (value[index] - '0') * weights[index];

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}

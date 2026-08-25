namespace Horafy.Application.Features.Auth;

/// <summary>
/// Normalização do celular usado como identificador de login/cadastro do cliente.
///
/// Persistimos e comparamos apenas dígitos (ex.: "5547988572233"); como o código
/// do país (55) é opcional para quem digita, a busca considera as variantes
/// com e sem o prefixo.
/// </summary>
public static class PhoneNumber
{
    /// <summary>Remove tudo que não for dígito. Null/whitespace vira string vazia.</summary>
    public static string Normalize(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : new string(raw.Where(char.IsDigit).ToArray());

    /// <summary>
    /// DDD + número já normalizado: 10–11 dígitos (fixo/celular) ou
    /// 12–13 com o prefixo 55.
    /// </summary>
    public static bool IsValid(string digits) =>
        digits.Length is >= 10 and <= 13;

    /// <summary>
    /// Variantes a considerar na busca: o valor informado e, quando aplicável,
    /// a forma equivalente com/sem o código do país 55.
    /// </summary>
    public static IReadOnlyList<string> BuildCandidates(string digits)
    {
        var candidates = new List<string>(2) { digits };

        if (!digits.StartsWith("55", StringComparison.Ordinal) && digits.Length is 10 or 11)
            candidates.Add("55" + digits);
        else if (digits.StartsWith("55", StringComparison.Ordinal) && digits.Length is 12 or 13)
            candidates.Add(digits[2..]);

        return candidates;
    }
}

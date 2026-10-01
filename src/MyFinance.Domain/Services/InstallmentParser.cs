using System.Globalization;
using System.Text.RegularExpressions;

namespace MyFinance.Domain.Services;

/// <summary>Parcela identificada na descrição: "Notebook - Parcela 3/12" → ("Notebook", 3, 12).</summary>
public sealed record InstallmentInfo(string BaseDescription, int Number, int Count);

/// <summary>
/// Reconhece parcelas no fim da descrição, nos formatos usados pelos bancos:
/// "Parcela 3/12", "PARC 03/12", "Parc. 3 de 12", "3 de 12" e "LOJA 03/12" (ambos com dois dígitos).
/// Exige 1 ≤ parcela ≤ total, 2 ≤ total ≤ <see cref="MaxInstallments"/>.
/// </summary>
public static partial class InstallmentParser
{
    public const int MaxInstallments = 48;

    public static InstallmentInfo? Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var text = description.Trim();
        foreach (var pattern in (ReadOnlySpan<Regex>)[WithKeyword(), WithDe(), TwoDigits()])
        {
            var match = pattern.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var number = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
            var count = int.Parse(match.Groups["c"].Value, CultureInfo.InvariantCulture);
            var baseDescription = text[..match.Index].TrimEnd(' ', '-', '–', '—', '|', ',', ';').Trim();

            if (count is < 2 or > MaxInstallments || number < 1 || number > count || baseDescription.Length == 0)
            {
                return null;
            }

            return new InstallmentInfo(baseDescription, number, count);
        }

        return null;
    }

    [GeneratedRegex(@"[\s\-–—|,;]*\bparc(?:ela)?\.?\s*(?<n>\d{1,2})\s*(?:/|de)\s*(?<c>\d{1,2})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WithKeyword();

    [GeneratedRegex(@"[\s\-–—|,;]+(?<n>\d{1,2})\s+de\s+(?<c>\d{1,2})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WithDe();

    [GeneratedRegex(@"[\s\-–—|,;]+(?<n>\d{2})/(?<c>\d{2})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TwoDigits();
}
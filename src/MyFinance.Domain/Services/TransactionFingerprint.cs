using System.Globalization;
using System.Text;

using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Services;

/// <summary>
/// Normalização e hash dos dados relevantes de uma transação, independentes do formato de origem.
/// </summary>
public static class TransactionFingerprint
{
    /// <summary>
    /// Maiúsculas, sem acentos e com espaços colapsados: "  Pão   de Açúcar " → "PAO DE ACUCAR".
    /// </summary>
    public static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var decomposed = description.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                previousWasSpace = builder.Length > 0;
                continue;
            }

            if (previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = false;
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Hash de data, valor, descrição normalizada e saldo (quando o arquivo informa o saldo após a transação,
    /// ele diferencia duas compras idênticas no mesmo dia).
    /// </summary>
    public static Sha256Hash ComputeImportHash(DateOnly date, decimal amount, string description, decimal? balance)
    {
        var payload = string.Join(
            '|',
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            NormalizeDescription(description),
            balance?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty);

        return Sha256Hash.Compute(payload);
    }
}
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyFinance.Infrastructure.Imports.Common;

/// <summary>
/// Converte valores monetários escritos em formato brasileiro ou americano:
/// <c>-120.50</c>, <c>-120,50</c>, <c>1.234,56</c>, <c>1,234.56</c>, <c>R$ -35,90</c>, <c>(35,90)</c>.
/// </summary>
internal static partial class AmountParser
{
    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("BRL", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        var negative = false;
        if (value.StartsWith('(') && value.EndsWith(')'))
        {
            negative = true;
            value = value[1..^1];
        }

        if (value.StartsWith('-'))
        {
            negative = !negative;
            value = value[1..];
        }
        else if (value.StartsWith('+'))
        {
            value = value[1..];
        }

        value = NormalizeSeparators(value);
        if (value is null || !Number().IsMatch(value))
        {
            return false;
        }

        amount = decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        if (negative)
        {
            amount = -amount;
        }

        return true;
    }

    /// <summary>Retorna o número com '.' como separador decimal e sem separador de milhar.</summary>
    private static string? NormalizeSeparators(string value)
    {
        var lastComma = value.LastIndexOf(',');
        var lastDot = value.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            // O separador que aparece por último é o decimal; o outro é de milhar.
            var (thousands, decimalSeparator) = lastComma > lastDot ? ('.', ',') : (',', '.');
            var integerPart = value[..Math.Max(lastComma, lastDot)];
            if (integerPart.Contains(decimalSeparator, StringComparison.Ordinal))
            {
                return null;
            }

            return value.Replace(thousands.ToString(), string.Empty, StringComparison.Ordinal).Replace(decimalSeparator, '.');
        }

        if (lastComma >= 0)
        {
            // Somente vírgula: decimal brasileiro ("120,50"). Mais de uma vírgula é ambíguo.
            return value.IndexOf(',') == lastComma && !LooksLikeThousands(value, lastComma) ? value.Replace(',', '.') : null;
        }

        if (lastDot >= 0 && value.IndexOf('.') != lastDot)
        {
            // Vários pontos sem vírgula: separadores de milhar ("1.234.567").
            return value.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        return lastDot >= 0 && LooksLikeThousands(value, lastDot) ? null : value;
    }

    /// <summary>
    /// "1.500" ou "1,500": separador único seguido de exatamente 3 dígitos pode ser milhar (1500) ou decimal (1,5).
    /// Ler errado grava um valor mil vezes menor sem aviso — por isso é rejeitado como ambíguo.
    /// </summary>
    private static bool LooksLikeThousands(string value, int separatorIndex) =>
        value.Length - separatorIndex - 1 == 3
        && separatorIndex is >= 1 and <= 3
        && value[0] != '0'
        && value.AsSpan(0, separatorIndex).IndexOfAnyExceptInRange('0', '9') < 0;

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex Number();
}
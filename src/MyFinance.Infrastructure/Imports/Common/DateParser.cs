using System.Globalization;

namespace MyFinance.Infrastructure.Imports.Common;

internal static class DateParser
{
    /// <summary>Formatos aceitos em CSV. Datas no padrão americano (MM/dd) não são suportadas por serem ambíguas.</summary>
    private static readonly string[] CsvFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy/MM/dd",
    ];

    /// <summary>Aceita também data e hora ("2026-09-29 10:15:00", "2026-09-29T10:15:00"), descartando o horário.</summary>
    public static bool TryParseCsv(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var datePart = text.Trim().Split([' ', 'T'], 2)[0];
        return DateOnly.TryParseExact(datePart, CsvFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>
    /// Datas OFX: <c>YYYYMMDD[HHMMSS[.XXX]][[offset:TZ]]</c>. Usa somente a data como informada pelo banco,
    /// sem conversão de fuso (a data do extrato é a data do lançamento).
    /// </summary>
    public static bool TryParseOfx(string? text, out DateOnly date)
    {
        date = default;
        var value = text?.Trim();
        return value is { Length: >= 8 }
            && DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
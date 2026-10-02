using System.Globalization;

using ModelContextProtocol;

using MyFinance.Application.Reports;
using MyFinance.Domain;

namespace MyFinance.Mcp.Tools;

/// <summary>Conversão e validação dos argumentos das ferramentas.</summary>
internal static class McpArgs
{
    private static readonly string[] MonthFormats = ["yyyy-MM", "yyyy-MM-dd"];

    /// <summary>Mês "aaaa-MM" (ou uma data "aaaa-MM-dd") como primeiro dia do mês; vazio = <c>null</c>.</summary>
    public static DateOnly? ParseMonth(string? value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value.Trim(), MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Months.Of(date)
            : throw new McpException($"Valor inválido em '{parameter}': use o formato aaaa-MM (ex.: 2026-09).");
    }

    public static string Month(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Período de/até; o que faltar termina na fatura atual e cobre <paramref name="defaultMonths"/> meses.</summary>
    public static async Task<(DateOnly From, DateOnly To)> PeriodAsync(
        ReportService reports, string? from, string? to, int defaultMonths, CancellationToken cancellationToken)
    {
        var start = ParseMonth(from, "de");
        var end = ParseMonth(to, "ate");
        if (start is { } s && end is { } e)
        {
            return (s, e);
        }

        if (end is { } onlyEnd)
        {
            return (onlyEnd.AddMonths(-(defaultMonths - 1)), onlyEnd);
        }

        var (defaultFrom, defaultTo) = await reports.LastMonthsAsync(defaultMonths, cancellationToken);
        if (start is { } onlyStart)
        {
            return (onlyStart, onlyStart > defaultTo ? onlyStart : defaultTo);
        }

        return (defaultFrom, defaultTo);
    }
}
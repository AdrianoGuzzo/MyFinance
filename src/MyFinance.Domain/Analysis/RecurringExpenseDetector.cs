using MyFinance.Domain.Enums;

namespace MyFinance.Domain.Analysis;

/// <summary>Gasto que se repete todo mês no mesmo estabelecimento (assinatura, academia...).</summary>
/// <param name="EstimatedMonthlyAmount">Mediana dos três últimos meses em que apareceu.</param>
public sealed record RecurringCandidate(
    string MerchantKey, string DisplayName, decimal EstimatedMonthlyAmount, Guid? CategoryId, DateOnly LastSeenMonth, int MonthsSeen);

/// <summary>
/// Detecta gastos recorrentes nos últimos <see cref="WindowMonths"/> meses:
/// <list type="bullet">
/// <item><description>compras (não parceladas) do mesmo estabelecimento em pelo menos <see cref="MinimumMonths"/> meses;</description></item>
/// <item><description>ainda ativo: presente no mês de referência ou no anterior;</description></item>
/// <item><description>cerca de uma cobrança por mês (no máximo uma a mais no período);</description></item>
/// <item><description>valor estável: todos os meses dentro de ±<see cref="Tolerance"/> da mediana.</description></item>
/// </list>
/// </summary>
public static class RecurringExpenseDetector
{
    public const int WindowMonths = 6;
    public const int MinimumMonths = 3;
    public const decimal Tolerance = 0.20m;

    public static IReadOnlyList<RecurringCandidate> Detect(IEnumerable<SpendingEntry> entries, DateOnly referenceMonth)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var last = Months.Of(referenceMonth);
        var first = last.AddMonths(-(WindowMonths - 1));

        return [.. entries
            .Where(e => e.Kind == TransactionKind.Purchase && e.InstallmentPurchaseId is null
                && e.InvoiceMonth >= first && e.InvoiceMonth <= last && e.MerchantKey.Length > 0)
            .GroupBy(e => e.MerchantKey)
            .Select(g => Evaluate(g.Key, [.. g], last))
            .OfType<RecurringCandidate>()
            .OrderByDescending(c => c.EstimatedMonthlyAmount)];
    }

    private static RecurringCandidate? Evaluate(string merchantKey, IReadOnlyList<SpendingEntry> items, DateOnly referenceMonth)
    {
        var byMonth = items
            .GroupBy(e => e.InvoiceMonth)
            .OrderBy(g => g.Key)
            .Select(g => (Month: g.Key, Amount: g.Sum(e => e.Spending)))
            .ToList();

        if (byMonth.Count < MinimumMonths
            || byMonth[^1].Month < referenceMonth.AddMonths(-1)
            || items.Count > byMonth.Count + 1)
        {
            return null;
        }

        var median = Median(byMonth.Select(m => m.Amount));
        if (median <= 0 || byMonth.Any(m => Math.Abs(m.Amount - median) > median * Tolerance))
        {
            return null;
        }

        var latest = items.OrderByDescending(e => e.Date).ThenByDescending(e => e.InvoiceMonth).First();
        return new RecurringCandidate(
            merchantKey,
            latest.MerchantName,
            decimal.Round(Median(byMonth.TakeLast(3).Select(m => m.Amount)), 2),
            latest.CategoryId,
            byMonth[^1].Month,
            byMonth.Count);
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
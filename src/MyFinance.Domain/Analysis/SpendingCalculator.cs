namespace MyFinance.Domain.Analysis;

public sealed record MonthTotal(DateOnly Month, decimal Amount);

/// <param name="Percent">Participação no total do mês (0,24 = 24%).</param>
public sealed record CategoryShare(CategoryKey Category, decimal Amount, decimal Percent);

/// <summary>
/// Cálculos básicos sobre os gastos (ADR 0012): o mês é a competência da fatura e o valor é o de gasto
/// (compras, tarifas e juros menos estornos; pagamentos de fatura não contam).
/// </summary>
public static class SpendingCalculator
{
    /// <summary>Quantidade de meses usada nas médias ("média dos últimos 6 meses").</summary>
    public const int AverageWindow = 6;

    public static decimal Total(IEnumerable<SpendingEntry> entries, DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var target = Months.Of(month);
        return entries.Where(e => e.InvoiceMonth == target).Sum(e => e.Spending);
    }

    public static IReadOnlyList<MonthTotal> Monthly(IEnumerable<SpendingEntry> entries, IReadOnlyList<DateOnly> months)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(months);

        var totals = entries.GroupBy(e => e.InvoiceMonth).ToDictionary(g => g.Key, g => g.Sum(e => e.Spending));
        return [.. months.Select(m => new MonthTotal(Months.Of(m), totals.GetValueOrDefault(Months.Of(m))))];
    }

    /// <summary>
    /// Meses anteriores a <paramref name="month"/> usados na média: até <paramref name="window"/> meses,
    /// sem recuar antes do primeiro mês com lançamentos (meses sem gasto dentro desse intervalo contam como zero).
    /// </summary>
    public static IReadOnlyList<DateOnly> BaselineMonths(DateOnly month, DateOnly? firstMonth, int window = AverageWindow)
    {
        if (firstMonth is not { } first)
        {
            return [];
        }

        var target = Months.Of(month);
        var available = Months.Between(Months.Of(first), target);
        return available <= 0 ? [] : Months.Ending(target.AddMonths(-1), Math.Min(window, available));
    }

    /// <summary>Média mensal dos meses de referência; <c>null</c> sem histórico.</summary>
    public static decimal? Average(IEnumerable<SpendingEntry> entries, IReadOnlyList<DateOnly> baselineMonths)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(baselineMonths);

        if (baselineMonths.Count == 0)
        {
            return null;
        }

        var months = baselineMonths.ToHashSet();
        return decimal.Round(entries.Where(e => months.Contains(e.InvoiceMonth)).Sum(e => e.Spending) / baselineMonths.Count, 2);
    }

    /// <summary>Variação relativa (0,108 = +10,8%); <c>null</c> quando não há base de comparação.</summary>
    public static decimal? Variation(decimal current, decimal? baseline) =>
        baseline is { } value && value != 0 ? (current - value) / value : null;

    /// <summary>Distribuição dos gastos do mês por categoria, do maior para o menor. Categorias com saldo de estornos ficam de fora.</summary>
    public static IReadOnlyList<CategoryShare> ByCategory(
        IEnumerable<SpendingEntry> entries, DateOnly month, CategoryLookup categories, CategoryLevel level)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(categories);

        var target = Months.Of(month);
        var groups = entries
            .Where(e => e.InvoiceMonth == target)
            .GroupBy(e => categories.Resolve(e.CategoryId, level))
            .Select(g => (Category: g.Key, Amount: g.Sum(e => e.Spending)))
            .Where(g => g.Amount > 0)
            .ToList();

        var total = groups.Sum(g => g.Amount);
        return [.. groups
            .OrderByDescending(g => g.Amount)
            .ThenBy(g => g.Category.Name, StringComparer.Ordinal)
            .Select(g => new CategoryShare(g.Category, g.Amount, total == 0 ? 0 : g.Amount / total))];
    }
}
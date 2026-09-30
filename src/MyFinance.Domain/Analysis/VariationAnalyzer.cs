namespace MyFinance.Domain.Analysis;

/// <summary>Base de comparação de um mês.</summary>
public enum ComparisonBase
{
    /// <summary>Mês anterior.</summary>
    PreviousMonth = 1,

    /// <summary>Média dos meses anteriores (até <see cref="SpendingCalculator.AverageWindow"/>).</summary>
    Average = 2,
}

/// <param name="Baseline">Gasto do mês anterior ou média, conforme a base de comparação.</param>
/// <param name="Percent">Variação relativa; <c>null</c> quando a base é zero (gasto novo).</param>
public sealed record CategoryComparison(CategoryKey Category, decimal Current, decimal Baseline, decimal Difference, decimal? Percent);

/// <summary>Limiares para destacar uma variação como relevante ("O que aumentou?").</summary>
public static class AnalysisThresholds
{
    /// <summary>Diferença mínima, em reais.</summary>
    public const decimal MinimumDifference = 30m;

    /// <summary>Variação relativa mínima (10%).</summary>
    public const decimal MinimumPercent = 0.10m;
}

/// <summary>"O que aumentou?" / "O que diminuiu?": compara o gasto de cada categoria no mês com uma base.</summary>
public static class VariationAnalyzer
{
    /// <param name="baselineMonths">Meses da base: o mês anterior ou os meses da média.</param>
    public static IReadOnlyList<CategoryComparison> Compare(
        IReadOnlyCollection<SpendingEntry> entries,
        DateOnly month,
        IReadOnlyList<DateOnly> baselineMonths,
        CategoryLookup categories,
        CategoryLevel level)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(baselineMonths);
        ArgumentNullException.ThrowIfNull(categories);

        var target = Months.Of(month);
        var baseline = baselineMonths.ToHashSet();

        return [.. entries
            .Where(e => e.InvoiceMonth == target || baseline.Contains(e.InvoiceMonth))
            .GroupBy(e => categories.Resolve(e.CategoryId, level))
            .Select(g =>
            {
                var current = g.Where(e => e.InvoiceMonth == target).Sum(e => e.Spending);
                var reference = baseline.Count == 0 ? 0 : decimal.Round(g.Where(e => baseline.Contains(e.InvoiceMonth)).Sum(e => e.Spending) / baseline.Count, 2);
                return new CategoryComparison(g.Key, current, reference, current - reference, SpendingCalculator.Variation(current, reference));
            })
            .OrderByDescending(c => c.Difference)];
    }

    /// <summary>Aumentos relevantes, do maior para o menor.</summary>
    public static IReadOnlyList<CategoryComparison> Increased(IEnumerable<CategoryComparison> comparisons) =>
        [.. comparisons.Where(c => IsRelevant(c) && c.Difference > 0).OrderByDescending(c => c.Difference)];

    /// <summary>Reduções relevantes, da maior para a menor.</summary>
    public static IReadOnlyList<CategoryComparison> Decreased(IEnumerable<CategoryComparison> comparisons) =>
        [.. comparisons.Where(c => IsRelevant(c) && c.Difference < 0).OrderBy(c => c.Difference)];

    private static bool IsRelevant(CategoryComparison comparison) =>
        Math.Abs(comparison.Difference) >= AnalysisThresholds.MinimumDifference
        && (comparison.Percent is null || Math.Abs(comparison.Percent.Value) >= AnalysisThresholds.MinimumPercent);
}
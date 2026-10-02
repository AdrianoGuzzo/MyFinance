using MyFinance.Application.Analysis;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Enums;

namespace MyFinance.Application.Reports;

/// <param name="MonthlyAverage">Total dividido pela quantidade de meses do período.</param>
/// <param name="Variation">Último mês do período em relação ao anterior; <c>null</c> sem base.</param>
/// <param name="CategoryId">Categoria do grupo; <c>null</c> = "Sem categoria".</param>
public sealed record CategoryReportRow(Guid? CategoryId, string Name, string Color, decimal Total, decimal Percent, decimal MonthlyAverage, decimal? Variation);

public sealed record MerchantReportRow(string Merchant, int Count, decimal Total, decimal Average);

/// <param name="Average">Média dos meses anteriores (até 6, a partir do primeiro mês com lançamentos).</param>
/// <param name="Variation">Em relação ao mês anterior.</param>
public sealed record MonthlyReportRow(DateOnly Month, decimal Total, decimal? Average, decimal? Variation);

/// <summary>Relatórios por período (meses de competência, inclusive).</summary>
public sealed class ReportService(ISpendingQueries spending, AnalysisLoader loader)
{
    public const int MaxMerchants = 100;

    /// <summary>Período dos últimos <paramref name="months"/> meses terminando na fatura atual.</summary>
    public async Task<(DateOnly From, DateOnly To)> LastMonthsAsync(int months, CancellationToken cancellationToken)
    {
        var to = await loader.DefaultMonthAsync(cancellationToken);
        return (to.AddMonths(-(months - 1)), to);
    }

    public async Task<IReadOnlyList<CategoryReportRow>> ByCategoryAsync(
        DateOnly from, DateOnly to, CategoryLevel level, CancellationToken cancellationToken)
    {
        var (start, end, months) = Period(from, to);
        var context = await loader.LoadAsync(end, months.Count, 0, cancellationToken);
        var entries = context.Entries.Where(e => e.InvoiceMonth >= start).ToList();
        var groups = entries
            .GroupBy(e => context.Categories.Resolve(e.CategoryId, level))
            .Select(g => (Category: g.Key, Total: g.Sum(e => e.Spending), Items: g.ToList()))
            .Where(g => g.Total > 0)
            .ToList();
        var total = groups.Sum(g => g.Total);

        return [.. groups
            .OrderByDescending(g => g.Total)
            .Select(g => new CategoryReportRow(
                g.Category.Id,
                g.Category.Name,
                g.Category.Color,
                g.Total,
                total == 0 ? 0 : g.Total / total,
                decimal.Round(g.Total / months.Count, 2),
                months.Count < 2 ? null : SpendingCalculator.Variation(
                    SpendingCalculator.Total(g.Items, end), SpendingCalculator.Total(g.Items, end.AddMonths(-1)))))];
    }

    public async Task<IReadOnlyList<MerchantReportRow>> ByMerchantAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (start, end, _) = Period(from, to);
        var entries = await spending.GetEntriesAsync(start, end, null, cancellationToken);

        return [.. entries
            .Where(e => e.Kind != TransactionKind.Payment)
            .GroupBy(e => e.MerchantKey)
            .Select(g => new MerchantReportRow(
                g.GroupBy(e => e.MerchantName).OrderByDescending(n => n.Count()).First().Key,
                g.Count(e => e.Kind == TransactionKind.Purchase),
                g.Sum(e => e.Spending),
                decimal.Round(g.Sum(e => e.Spending) / Math.Max(1, g.Count(e => e.Kind == TransactionKind.Purchase)), 2)))
            .Where(r => r.Total > 0)
            .OrderByDescending(r => r.Total)
            .Take(MaxMerchants)];
    }

    public async Task<IReadOnlyList<MonthlyReportRow>> EvolutionAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (_, end, months) = Period(from, to);
        var context = await loader.LoadAsync(end, months.Count + SpendingCalculator.AverageWindow, 0, cancellationToken);

        return [.. months.Select(month =>
        {
            var total = SpendingCalculator.Total(context.Entries, month);
            var average = SpendingCalculator.Average(context.Entries, SpendingCalculator.BaselineMonths(month, context.FirstMonth));
            var previous = context.FirstMonth is { } first && first < month ? SpendingCalculator.Total(context.Entries, month.AddMonths(-1)) : (decimal?)null;
            return new MonthlyReportRow(month, total, average, SpendingCalculator.Variation(total, previous));
        })];
    }

    private static (DateOnly Start, DateOnly End, IReadOnlyList<DateOnly> Months) Period(DateOnly from, DateOnly to)
    {
        var start = Months.Of(from);
        var end = Months.Of(to);
        if (end < start)
        {
            throw new ValidationException("O mês final deve ser igual ou posterior ao inicial.");
        }

        return (start, end, Months.Ending(end, Months.Between(start, end) + 1));
    }
}
using MyFinance.Application.Analysis;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Strategy;

/// <summary>Consumo de um limite no mês analisado.</summary>
public sealed record LimitUsage(SpendingLimit Limit, CategoryKey Category, LimitEvaluation Evaluation);

/// <summary>Gasto recorrente detectado com a decisão do usuário (se houver).</summary>
public sealed record RecurringItem(RecurringCandidate Candidate, RecurringExpense? Saved)
{
    public RecurringClassification Classification => Saved?.Classification ?? RecurringClassification.Unclassified;

    public bool IsDismissed => Saved?.IsDismissed ?? false;
}

/// <summary>
/// Cálculos compartilhados pelo dashboard e pela estratégia: consumo dos limites, gastos recorrentes
/// e oportunidades de economia do mês analisado. Somente leitura.
/// </summary>
public sealed class SavingsAnalysis(ISpendingLimitRepository limits, IRecurringExpenseRepository recurring)
{
    public async Task<IReadOnlyList<LimitUsage>> LimitsAsync(AnalysisContext context, bool includeInactive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var monthEntries = context.Entries.Where(e => e.InvoiceMonth == context.ReferenceMonth).ToList();
        return [.. (await limits.ListAsync(includeInactive, cancellationToken)).Select(limit =>
        {
            var ids = context.Categories.WithChildren(limit.CategoryId);
            var used = monthEntries.Where(e => e.CategoryId is { } id && ids.Contains(id)).Sum(e => e.Spending);
            return new LimitUsage(limit, context.Categories.Resolve(limit.CategoryId, CategoryLevel.Leaf), limit.Evaluate(used));
        })];
    }

    /// <summary>Gastos recorrentes detectados agora, com a classificação salva pelo usuário.</summary>
    public async Task<IReadOnlyList<RecurringItem>> RecurringAsync(AnalysisContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var saved = (await recurring.ListAsync(cancellationToken)).ToDictionary(r => r.MerchantKey, StringComparer.Ordinal);
        return [.. RecurringExpenseDetector.Detect(context.Entries, context.ReferenceMonth)
            .Select(c => new RecurringItem(c, saved.GetValueOrDefault(c.MerchantKey)))];
    }

    public async Task<IReadOnlyList<SavingsOpportunity>> OpportunitiesAsync(AnalysisContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var versusAverage = VariationAnalyzer.Compare(context.Entries, context.ReferenceMonth, context.BaselineMonths, context.Categories, CategoryLevel.Leaf);
        var exceeded = (await LimitsAsync(context, includeInactive: false, cancellationToken))
            .Where(l => l.Evaluation.Status == LimitStatus.Exceeded)
            .Select(l => new ExceededLimit(l.Category, l.Evaluation.Limit, l.Evaluation.Used))
            .ToList();

        return SavingsOpportunityFinder.Find(new OpportunityInput(
            context.BaselineMonths.Count, versusAverage, exceeded, Summarize(await RecurringAsync(context, cancellationToken))));
    }

    public static RecurringSummary Summarize(IReadOnlyList<RecurringItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var active = items.Where(i => !i.IsDismissed).ToList();
        var optional = active.Where(i => i.Classification is RecurringClassification.Optional or RecurringClassification.Evaluate).ToList();
        return new RecurringSummary(
            optional.Count,
            optional.Sum(i => i.Candidate.EstimatedMonthlyAmount),
            active.Count,
            active.Sum(i => i.Candidate.EstimatedMonthlyAmount));
    }
}
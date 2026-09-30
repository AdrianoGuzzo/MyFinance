using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Installments;
using MyFinance.Domain;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Dashboard;

public sealed record CardInvoiceDto(Guid CreditCardId, string CreditCardName, CurrentInvoiceDto Invoice);

/// <param name="Percent">Participação no gasto do mês (0,24 = 24%).</param>
/// <param name="VersusPreviousMonth">Variação em relação ao mês anterior; <c>null</c> sem base.</param>
/// <param name="VersusAverage">Variação em relação à média; <c>null</c> sem histórico.</param>
public sealed record CategorySpendingDto(
    Guid? CategoryId, string Name, string Color, decimal Amount, decimal Percent, decimal? VersusPreviousMonth, decimal? VersusAverage);

/// <param name="Baseline">Gasto do mês anterior ou média, conforme a comparação.</param>
public sealed record VariationDto(Guid? CategoryId, string Name, decimal Current, decimal Baseline, decimal Difference, decimal? Percent);

public sealed record MonthlySpendingDto(DateOnly Month, decimal Amount);

/// <summary>Próxima fatura: o que já foi lançado mais as parcelas já compromissadas.</summary>
public sealed record InvoiceProjectionDto(DateOnly Month, decimal Posted, decimal Installments)
{
    public decimal Total => Posted + Installments;
}

public sealed record InsightDto(InsightTone Tone, string Message);

/// <summary>Possível oportunidade de economia (informação, não recomendação).</summary>
public sealed record OpportunityDto(string Title, string Detail, decimal MonthlyPotential);

public sealed record GoalProgressDto(string Name, decimal MonthlyTarget, decimal IdentifiedPotential);

/// <summary>"Como estou gastando meu dinheiro?" — visão do mês de referência (competência da fatura).</summary>
/// <param name="IsPartial">Alguma fatura do mês ainda está aberta: o gasto ainda pode crescer.</param>
/// <param name="Average">Média mensal dos meses anteriores (até 6); <c>null</c> sem histórico.</param>
/// <param name="HistoryMonths">Quantidade de meses usada na média.</param>
/// <param name="CurrentInvoiceTotal">Soma das faturas abertas dos cartões ativos.</param>
/// <param name="OutstandingInstallments">Parcelas ainda não lançadas a partir do mês de referência.</param>
public sealed record DashboardDto(
    DateOnly ReferenceMonth,
    bool IsPartial,
    decimal MonthSpending,
    decimal? Average,
    decimal? Variation,
    int HistoryMonths,
    decimal CurrentInvoiceTotal,
    IReadOnlyList<CardInvoiceDto> OpenInvoices,
    IReadOnlyList<InvoiceProjectionDto> NextInvoices,
    IReadOnlyList<CategorySpendingDto> Categories,
    IReadOnlyList<VariationDto> IncreasedVsPreviousMonth,
    IReadOnlyList<VariationDto> DecreasedVsPreviousMonth,
    IReadOnlyList<VariationDto> IncreasedVsAverage,
    IReadOnlyList<VariationDto> DecreasedVsAverage,
    IReadOnlyList<MonthlySpendingDto> Evolution,
    decimal OutstandingInstallments,
    IReadOnlyList<OpportunityDto> Opportunities,
    GoalProgressDto? Goal,
    IReadOnlyList<InsightDto> Insights);

/// <summary>
/// Visão geral dos gastos. Todas as análises usam a competência da fatura (mês de referência) e o valor de gasto
/// de cada lançamento (compras, tarifas e juros menos estornos; pagamentos de fatura não contam) — ver ADR 0012.
/// </summary>
public sealed class DashboardService(
    ICreditCardRepository creditCards,
    AnalysisLoader loader,
    InstallmentService installments,
    TimeProvider timeProvider)
{
    public const int DefaultEvolutionMonths = 6;
    public const int NextInvoicesMonths = 4;

    /// <param name="month">Mês de referência; <c>null</c> = fatura atual.</param>
    /// <param name="evolutionMonths">Meses do gráfico de evolução (3, 6, 12...).</param>
    public async Task<DashboardDto> GetAsync(DateOnly? month, int evolutionMonths, CancellationToken cancellationToken)
    {
        var reference = month is { } chosen ? Months.Of(chosen) : await loader.DefaultMonthAsync(cancellationToken);
        var window = Math.Max(evolutionMonths, SpendingCalculator.AverageWindow);
        var context = await loader.LoadAsync(reference, window, NextInvoicesMonths, cancellationToken);
        var entries = context.Entries;
        var baseline = context.BaselineMonths;

        var monthSpending = SpendingCalculator.Total(entries, reference);
        var average = SpendingCalculator.Average(entries, baseline);

        var previous = context.FirstMonth is { } first && first < reference ? [reference.AddMonths(-1)] : Array.Empty<DateOnly>();
        var versusPrevious = VariationAnalyzer.Compare(entries, reference, previous, context.Categories, CategoryLevel.Root);
        var versusAverage = VariationAnalyzer.Compare(entries, reference, baseline, context.Categories, CategoryLevel.Root);

        var (purchases, posted) = await installments.LoadAsync(cancellationToken);
        var nextMonths = Months.Ending(reference.AddMonths(NextInvoicesMonths - 1), NextInvoicesMonths);
        var commitments = CommitmentProjector.Project(purchases, nextMonths, posted).ToDictionary(c => c.Month, c => c.Amount);
        var outstanding = CommitmentProjector.Outstanding(purchases, reference, posted);

        var openInvoices = await OpenInvoicesAsync(entries, cancellationToken);
        var insights = InsightGenerator.Generate(new InsightInput(
            baseline.Count, versusAverage, previous.Length == 0 ? [] : versusPrevious, outstanding, RecurringCount: 0, RecurringMonthly: 0m));

        return new DashboardDto(
            reference,
            await loader.IsOpenAsync(reference, cancellationToken),
            monthSpending,
            average,
            SpendingCalculator.Variation(monthSpending, average),
            baseline.Count,
            openInvoices.Sum(i => i.Invoice.Amount),
            openInvoices,
            [.. nextMonths.Select(m => new InvoiceProjectionDto(m, SpendingCalculator.Total(entries, m), commitments.GetValueOrDefault(m)))],
            Categories(context, versusPrevious, versusAverage, previous.Length > 0),
            previous.Length == 0 ? [] : ToDto(VariationAnalyzer.Increased(versusPrevious)),
            previous.Length == 0 ? [] : ToDto(VariationAnalyzer.Decreased(versusPrevious)),
            ToDto(VariationAnalyzer.Increased(versusAverage)),
            ToDto(VariationAnalyzer.Decreased(versusAverage)),
            [.. SpendingCalculator.Monthly(entries, Months.Ending(reference, evolutionMonths)).Select(m => new MonthlySpendingDto(m.Month, m.Amount))],
            outstanding,
            Opportunities: [],
            Goal: null,
            [.. insights.Select(i => new InsightDto(i.Tone, i.Message))]);
    }

    public Task<DashboardDto> GetAsync(CancellationToken cancellationToken) => GetAsync(null, DefaultEvolutionMonths, cancellationToken);

    private async Task<IReadOnlyList<CardInvoiceDto>> OpenInvoicesAsync(IReadOnlyList<SpendingEntry> entries, CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        return [.. (await creditCards.ListAsync(includeInactive: false, cancellationToken)).Select(card =>
        {
            var period = card.GetCurrentInvoicePeriod(today);
            var amount = entries.Where(e => e.CreditCardId == card.Id && e.InvoiceMonth == period.ReferenceMonth).Sum(e => e.Spending);
            return new CardInvoiceDto(card.Id, card.Name, new CurrentInvoiceDto(period.ReferenceMonth, period.ClosingDate, period.DueDate, amount));
        })];
    }

    private static IReadOnlyList<CategorySpendingDto> Categories(
        AnalysisContext context, IReadOnlyList<CategoryComparison> versusPrevious, IReadOnlyList<CategoryComparison> versusAverage, bool hasPrevious)
    {
        var previous = versusPrevious.ToDictionary(c => c.Category);
        var average = versusAverage.ToDictionary(c => c.Category);

        return [.. SpendingCalculator.ByCategory(context.Entries, context.ReferenceMonth, context.Categories, CategoryLevel.Root)
            .Select(s => new CategorySpendingDto(
                s.Category.Id,
                s.Category.Name,
                s.Category.Color,
                s.Amount,
                s.Percent,
                hasPrevious ? previous.GetValueOrDefault(s.Category)?.Percent : null,
                context.BaselineMonths.Count > 0 ? average.GetValueOrDefault(s.Category)?.Percent : null))];
    }

    private static IReadOnlyList<VariationDto> ToDto(IEnumerable<CategoryComparison> comparisons) =>
        [.. comparisons.Select(c => new VariationDto(c.Category.Id, c.Category.Name, c.Current, c.Baseline, c.Difference, c.Percent))];
}
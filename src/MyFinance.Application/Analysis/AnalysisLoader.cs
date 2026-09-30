using MyFinance.Application.Common;
using MyFinance.Domain;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Analysis;

/// <summary>Dados de uma análise: lançamentos de um intervalo de meses (competência) e categorias.</summary>
/// <param name="FirstMonth">Primeiro mês com lançamentos (limita as médias).</param>
public sealed record AnalysisContext(
    DateOnly ReferenceMonth,
    DateOnly? FirstMonth,
    IReadOnlyList<SpendingEntry> Entries,
    CategoryLookup Categories)
{
    /// <summary>Meses anteriores ao de referência usados na média.</summary>
    public IReadOnlyList<DateOnly> BaselineMonths => SpendingCalculator.BaselineMonths(ReferenceMonth, FirstMonth);
}

/// <summary>Carrega os dados comuns às análises (dashboard, relatórios, estratégia).</summary>
public sealed class AnalysisLoader(
    ICreditCardRepository creditCards,
    ICategoryRepository categories,
    ISpendingQueries spending,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Mês analisado por padrão: a fatura aberta mais próxima do vencimento entre os cartões ativos
    /// (a "fatura atual"); sem cartões, o mês corrente.
    /// </summary>
    public async Task<DateOnly> DefaultMonthAsync(CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var cards = await creditCards.ListAsync(includeInactive: false, cancellationToken);
        return cards.Count == 0 ? Months.Of(today) : cards.Min(c => c.GetCurrentInvoicePeriod(today).ReferenceMonth);
    }

    /// <summary>Indica se alguma fatura do mês ainda está aberta (o gasto do mês ainda pode crescer).</summary>
    public async Task<bool> IsOpenAsync(DateOnly month, CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var target = Months.Of(month);
        return (await creditCards.ListAsync(includeInactive: false, cancellationToken))
            .Any(c => today < c.GetInvoicePeriodForMonth(target).ClosingDate);
    }

    /// <param name="monthsBefore">Meses anteriores ao de referência a carregar (histórico).</param>
    /// <param name="monthsAfter">Meses posteriores a carregar (faturas futuras já com lançamentos).</param>
    public async Task<AnalysisContext> LoadAsync(DateOnly month, int monthsBefore, int monthsAfter, CancellationToken cancellationToken)
    {
        var reference = Months.Of(month);
        var entries = await spending.GetEntriesAsync(reference.AddMonths(-monthsBefore), reference.AddMonths(monthsAfter), null, cancellationToken);
        var lookup = new CategoryLookup(await categories.ListAsync(includeInactive: true, cancellationToken));
        return new AnalysisContext(reference, await spending.GetFirstMonthAsync(cancellationToken), entries, lookup);
    }
}
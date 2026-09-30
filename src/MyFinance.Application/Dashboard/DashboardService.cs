using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Application.CreditCards;
using MyFinance.Domain;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Dashboard;

public sealed record CardInvoiceDto(Guid CreditCardId, string CreditCardName, CurrentInvoiceDto Invoice);

public sealed record CategoryAmountDto(Guid? CategoryId, string CategoryName, string Color, decimal Amount);

public sealed record MonthlySpendingDto(DateOnly Month, decimal Amount);

public sealed record DashboardDto(
    DateOnly ReferenceMonth,
    decimal MonthSpending,
    decimal CurrentInvoiceTotal,
    IReadOnlyList<CardInvoiceDto> Invoices,
    IReadOnlyList<CategoryAmountDto> SpendingByCategory,
    IReadOnlyList<MonthlySpendingDto> MonthlySpending);

/// <summary>
/// Visão geral dos gastos. Todas as análises usam a competência da fatura (mês de referência)
/// e o valor de gasto de cada lançamento (compras, tarifas e juros menos estornos; pagamentos não contam).
/// </summary>
public sealed class DashboardService(
    ICreditCardRepository creditCards,
    ICategoryRepository categories,
    ISpendingQueries spending,
    TimeProvider timeProvider)
{
    public const int HistoryMonths = 6;

    internal const string UncategorizedColor = "#95A5A6";

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var cards = await creditCards.ListAsync(includeInactive: false, cancellationToken);
        var openPeriods = cards.ToDictionary(c => c.Id, c => c.GetCurrentInvoicePeriod(today));
        var referenceMonth = openPeriods.Count > 0 ? openPeriods.Values.Min(p => p.ReferenceMonth) : Months.Of(today);

        var months = Months.Ending(referenceMonth, HistoryMonths);
        var lastOpen = openPeriods.Count > 0 ? openPeriods.Values.Max(p => p.ReferenceMonth) : referenceMonth;
        var entries = await spending.GetEntriesAsync(months[0], lastOpen > referenceMonth ? lastOpen : referenceMonth, null, cancellationToken);
        var categoriesById = (await categories.ListAsync(includeInactive: true, cancellationToken)).ToDictionary(c => c.Id);

        var invoices = cards.Select(card =>
        {
            var period = openPeriods[card.Id];
            var amount = entries.Where(e => e.CreditCardId == card.Id && e.InvoiceMonth == period.ReferenceMonth).Sum(e => e.Spending);
            return new CardInvoiceDto(card.Id, card.Name, new CurrentInvoiceDto(period.ReferenceMonth, period.ClosingDate, period.DueDate, amount));
        }).ToList();

        var inMonth = entries.Where(e => e.InvoiceMonth == referenceMonth).ToList();

        return new DashboardDto(
            referenceMonth,
            inMonth.Sum(e => e.Spending),
            invoices.Sum(i => i.Invoice.Amount),
            invoices,
            ByRootCategory(inMonth, categoriesById),
            [.. months.Select(m => new MonthlySpendingDto(m, entries.Where(e => e.InvoiceMonth == m).Sum(e => e.Spending)))]);
    }

    /// <summary>Gastos agrupados pela categoria principal (subcategorias somam no pai).</summary>
    private static IReadOnlyList<CategoryAmountDto> ByRootCategory(
        IEnumerable<SpendingEntry> entries, Dictionary<Guid, Category> categoriesById) =>
        [.. entries
            .GroupBy(e => RootCategory(e.CategoryId, categoriesById)?.Id)
            .Select(g =>
            {
                var root = g.Key is { } id ? categoriesById[id] : null;
                return new CategoryAmountDto(g.Key, root?.Name ?? "Sem categoria", root?.Color?.Value ?? UncategorizedColor, g.Sum(e => e.Spending));
            })
            .Where(c => c.Amount > 0)
            .OrderByDescending(c => c.Amount)];

    private static Category? RootCategory(Guid? categoryId, Dictionary<Guid, Category> categoriesById)
    {
        if (categoryId is not { } id || !categoriesById.TryGetValue(id, out var category))
        {
            return null;
        }

        return category.ParentCategoryId is { } parentId && categoriesById.TryGetValue(parentId, out var parent) ? parent : category;
    }
}
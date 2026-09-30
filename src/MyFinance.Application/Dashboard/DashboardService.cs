using MyFinance.Application.CreditCards;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Dashboard;

public sealed record CardInvoiceDto(Guid CreditCardId, string CreditCardName, InvoiceDto Invoice);

public sealed record CategoryAmountDto(Guid? CategoryId, string CategoryName, string Color, decimal Amount);

public sealed record MonthlyTotalsDto(DateOnly Month, decimal Income, decimal Expenses);

public sealed record BalancePointDto(DateOnly Date, decimal Balance);

public sealed record DashboardDto(
    DateOnly ReferenceMonth,
    decimal TotalBalance,
    decimal MonthIncome,
    decimal MonthExpenses,
    decimal CurrentInvoiceTotal,
    IReadOnlyList<CardInvoiceDto> Invoices,
    IReadOnlyList<CategoryAmountDto> ExpensesByCategory,
    IReadOnlyList<MonthlyTotalsDto> IncomeVsExpenses,
    IReadOnlyList<BalancePointDto> BalanceEvolution)
{
    public decimal MonthBalance => MonthIncome - MonthExpenses;
}

/// <summary>
/// Regras do dashboard (ver docs/architecture.md):
/// <list type="bullet">
/// <item><description>Saldo total: contas ativas (saldo inicial + lançamentos). Cartões não entram no saldo.</description></item>
/// <item><description>Receitas: entradas em contas. Despesas: saídas em contas e compras no cartão, pela data da compra.</description></item>
/// <item><description>Lançamentos em categorias do tipo Transferência (ex.: pagamento de fatura) não são receita nem despesa.</description></item>
/// <item><description>Valores positivos no cartão (pagamentos, estornos) não são receita.</description></item>
/// </list>
/// </summary>
public sealed class DashboardService(
    IAccountRepository accounts,
    ICreditCardRepository creditCards,
    ICategoryRepository categories,
    ITransactionQueries queries,
    TimeProvider timeProvider)
{
    public const int HistoryMonths = 6;

    private const string UncategorizedColor = "#95A5A6";

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var historyStart = currentMonth.AddMonths(-(HistoryMonths - 1));
        var currentMonthEnd = currentMonth.AddMonths(1).AddDays(-1);

        var activeAccounts = await accounts.ListAsync(includeInactive: false, cancellationToken);
        var activeAccountIds = activeAccounts.Select(a => a.Id).ToHashSet();
        var categoriesById = (await categories.ListAsync(includeInactive: true, cancellationToken)).ToDictionary(c => c.Id);
        var snapshots = await queries.GetSnapshotsAsync(historyStart, currentMonthEnd, cancellationToken);

        var totalsAll = await queries.GetAccountTotalsAsync(null, cancellationToken);
        var totalBalance = activeAccounts.Sum(a => a.BalanceWith(totalsAll.GetValueOrDefault(a.Id)));

        bool IsTransfer(TransactionSnapshot s) =>
            s.CategoryId is { } id && categoriesById.TryGetValue(id, out var c) && c.Type == CategoryType.Transfer;

        var countable = snapshots.Where(s => !IsTransfer(s)).ToList();

        var monthly = Enumerable.Range(0, HistoryMonths)
            .Select(i => historyStart.AddMonths(i))
            .Select(month =>
            {
                var inMonth = countable.Where(s => s.Date.Year == month.Year && s.Date.Month == month.Month).ToList();
                return new MonthlyTotalsDto(month, Income(inMonth), Expenses(inMonth));
            })
            .ToList();

        var current = monthly[^1];
        var invoices = await GetInvoicesAsync(today, cancellationToken);

        return new DashboardDto(
            currentMonth,
            totalBalance,
            current.Income,
            current.Expenses,
            invoices.Sum(i => i.Invoice.Amount),
            invoices,
            ExpensesByCategory(countable.Where(s => s.Date >= currentMonth), categoriesById),
            monthly,
            await BalanceEvolutionAsync(activeAccounts, activeAccountIds, snapshots, historyStart, cancellationToken));
    }

    private static decimal Income(IEnumerable<TransactionSnapshot> snapshots) =>
        snapshots.Where(s => s.AccountId is not null && s.Amount > 0).Sum(s => s.Amount);

    private static decimal Expenses(IEnumerable<TransactionSnapshot> snapshots) =>
        -snapshots.Where(s => s.Amount < 0).Sum(s => s.Amount);

    /// <summary>Despesas do mês agrupadas pela categoria principal (subcategorias somam no pai).</summary>
    private static IReadOnlyList<CategoryAmountDto> ExpensesByCategory(
        IEnumerable<TransactionSnapshot> snapshots, Dictionary<Guid, Category> categoriesById) =>
        [.. snapshots
            .Where(s => s.Amount < 0)
            .GroupBy(s => RootCategory(s.CategoryId, categoriesById)?.Id)
            .Select(g =>
            {
                var root = g.Key is { } id ? categoriesById[id] : null;
                return new CategoryAmountDto(g.Key, root?.Name ?? "Sem categoria", root?.Color?.Value ?? UncategorizedColor, -g.Sum(s => s.Amount));
            })
            .OrderByDescending(c => c.Amount)];

    private static Category? RootCategory(Guid? categoryId, Dictionary<Guid, Category> categoriesById)
    {
        if (categoryId is not { } id || !categoriesById.TryGetValue(id, out var category))
        {
            return null;
        }

        return category.ParentCategoryId is { } parentId && categoriesById.TryGetValue(parentId, out var parent) ? parent : category;
    }

    /// <summary>Saldo das contas ativas ao fim de cada mês do histórico.</summary>
    private async Task<IReadOnlyList<BalancePointDto>> BalanceEvolutionAsync(
        IReadOnlyList<Account> activeAccounts,
        HashSet<Guid> activeAccountIds,
        IReadOnlyList<TransactionSnapshot> snapshots,
        DateOnly historyStart,
        CancellationToken cancellationToken)
    {
        var totalsBefore = await queries.GetAccountTotalsAsync(historyStart.AddDays(-1), cancellationToken);
        var running = activeAccounts.Sum(a => a.BalanceWith(totalsBefore.GetValueOrDefault(a.Id)));
        var accountSnapshots = snapshots.Where(s => s.AccountId is { } id && activeAccountIds.Contains(id)).ToList();

        var points = new List<BalancePointDto>();
        for (var i = 0; i < HistoryMonths; i++)
        {
            var month = historyStart.AddMonths(i);
            running += accountSnapshots.Where(s => s.Date.Year == month.Year && s.Date.Month == month.Month).Sum(s => s.Amount);
            points.Add(new BalancePointDto(month.AddMonths(1).AddDays(-1), running));
        }

        return points;
    }

    private async Task<IReadOnlyList<CardInvoiceDto>> GetInvoicesAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var result = new List<CardInvoiceDto>();
        foreach (var card in await creditCards.ListAsync(includeInactive: false, cancellationToken))
        {
            var invoice = await CreditCardService.GetInvoiceAsync(card, card.GetCurrentInvoicePeriod(today), queries, cancellationToken);
            result.Add(new CardInvoiceDto(card.Id, card.Name, invoice));
        }

        return result;
    }
}
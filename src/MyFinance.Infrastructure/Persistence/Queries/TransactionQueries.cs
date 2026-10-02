using Microsoft.EntityFrameworkCore;

using MyFinance.Application.Common;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;

namespace MyFinance.Infrastructure.Persistence.Queries;

internal sealed class TransactionQueries(FinanceDbContext db) : ITransactionQueries
{
    private const int MaxPageSize = 500;

    public async Task<PagedResult<TransactionListItem>> SearchAsync(TransactionSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var query =
            from t in db.Transactions.AsNoTracking()
            join i in db.Invoices on t.InvoiceId equals i.Id
            select new { Transaction = t, i.ReferenceMonth };

        if (search.CreditCardId is { } creditCardId)
        {
            query = query.Where(x => x.Transaction.CreditCardId == creditCardId);
        }

        if (search.InvoiceId is { } invoiceId)
        {
            query = query.Where(x => x.Transaction.InvoiceId == invoiceId);
        }

        if (search.FromMonth is { } from)
        {
            query = query.Where(x => x.ReferenceMonth >= from);
        }

        if (search.ToMonth is { } to)
        {
            query = query.Where(x => x.ReferenceMonth <= to);
        }

        if (search.Kind is { } kind)
        {
            query = query.Where(x => x.Transaction.Kind == kind);
        }

        if (search.ExcludePayments)
        {
            query = query.Where(x => x.Transaction.Kind != TransactionKind.Payment);
        }

        if (search.UncategorizedOnly)
        {
            query = query.Where(x => x.Transaction.CategoryId == null);
        }
        else if (search.CategoryId is { } categoryId)
        {
            var ids = await db.Categories
                .Where(c => c.Id == categoryId || c.ParentCategoryId == categoryId)
                .Select(c => (Guid?)c.Id)
                .ToListAsync(cancellationToken);
            query = query.Where(x => ids.Contains(x.Transaction.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = $"%{EscapeLike(search.Text.Trim())}%";
            query = query.Where(x => EF.Functions.Like(x.Transaction.Description, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.Transaction.Date)
            .ThenByDescending(x => x.Transaction.CreatedAt)
            .ThenByDescending(x => x.Transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Transaction.Id,
                x.Transaction.Date,
                x.ReferenceMonth,
                x.Transaction.Description,
                x.Transaction.MerchantName,
                x.Transaction.Amount,
                x.Transaction.Kind,
                x.Transaction.CreditCardId,
                x.Transaction.CategoryId,
                x.Transaction.InstallmentPurchaseId,
                x.Transaction.InstallmentNumber,
            })
            .ToListAsync(cancellationToken);

        // Tabelas de apoio são pequenas; resolver nomes em memória mantém a consulta principal simples.
        var cardNames = await db.CreditCards.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var purchaseIds = rows.Where(r => r.InstallmentPurchaseId is not null).Select(r => r.InstallmentPurchaseId!.Value).Distinct().ToList();
        var installmentCounts = await db.InstallmentPurchases.AsNoTracking()
            .Where(p => purchaseIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.InstallmentCount, cancellationToken);

        var items = rows.Select(t =>
        {
            var category = t.CategoryId is { } c ? categories.GetValueOrDefault(c) : null;
            var parent = category?.ParentCategoryId is { } p ? categories.GetValueOrDefault(p) : null;
            var categoryName = category is null ? null : parent is null ? category.Name : $"{parent.Name} > {category.Name}";
            int? count = t.InstallmentPurchaseId is { } purchaseId ? installmentCounts.GetValueOrDefault(purchaseId) : null;

            return new TransactionListItem(
                t.Id, t.Date, t.ReferenceMonth, t.Description, t.MerchantName, t.Amount, Spending.AmountOf(t.Kind, t.Amount), t.Kind,
                t.CreditCardId, cardNames.GetValueOrDefault(t.CreditCardId, "?"),
                t.CategoryId, categoryName, category?.Color?.Value ?? parent?.Color?.Value,
                t.InstallmentNumber, count);
        }).ToList();

        return new PagedResult<TransactionListItem>(items, total, page, pageSize);
    }

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
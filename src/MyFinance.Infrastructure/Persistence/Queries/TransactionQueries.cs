using Microsoft.EntityFrameworkCore;

using MyFinance.Application.Common;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Infrastructure.Persistence.Queries;

internal sealed class TransactionQueries(FinanceDbContext db) : ITransactionQueries
{
    private const int MaxPageSize = 500;

    public async Task<PagedResult<TransactionListItem>> SearchAsync(TransactionSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var query = db.Transactions.AsNoTracking();

        if (search.AccountId is { } accountId)
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        if (search.CreditCardId is { } creditCardId)
        {
            query = query.Where(t => t.CreditCardId == creditCardId);
        }

        if (search.From is { } from)
        {
            query = query.Where(t => t.Date >= from);
        }

        if (search.To is { } to)
        {
            query = query.Where(t => t.Date <= to);
        }

        if (search.UncategorizedOnly)
        {
            query = query.Where(t => t.CategoryId == null);
        }
        else if (search.CategoryId is { } categoryId)
        {
            var ids = await db.Categories
                .Where(c => c.Id == categoryId || c.ParentCategoryId == categoryId)
                .Select(c => (Guid?)c.Id)
                .ToListAsync(cancellationToken);
            query = query.Where(t => ids.Contains(t.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = $"%{EscapeLike(search.Text.Trim())}%";
            query = query.Where(t => EF.Functions.Like(t.Description, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new { t.Id, t.Date, t.Description, t.Amount, t.TransactionType, t.AccountId, t.CreditCardId, t.CategoryId })
            .ToListAsync(cancellationToken);

        // Tabelas de apoio são pequenas; resolver nomes em memória mantém a consulta principal simples.
        var accountNames = await db.Accounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);
        var cardNames = await db.CreditCards.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);

        var items = rows.Select(t =>
        {
            var (ownerType, ownerId, ownerName) = t.AccountId is { } a
                ? (TransactionOwnerType.Account, a, accountNames.GetValueOrDefault(a, "?"))
                : (TransactionOwnerType.CreditCard, t.CreditCardId!.Value, cardNames.GetValueOrDefault(t.CreditCardId!.Value, "?"));

            var category = t.CategoryId is { } c ? categories.GetValueOrDefault(c) : null;
            var parent = category?.ParentCategoryId is { } p ? categories.GetValueOrDefault(p) : null;
            var categoryName = category is null ? null : parent is null ? category.Name : $"{parent.Name} > {category.Name}";

            return new TransactionListItem(
                t.Id, t.Date, t.Description, t.Amount, t.TransactionType,
                ownerType, ownerId, ownerName,
                t.CategoryId, categoryName, category?.Color?.Value ?? parent?.Color?.Value);
        }).ToList();

        return new PagedResult<TransactionListItem>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<TransactionSnapshot>> GetSnapshotsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        await db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= fromDate && t.Date <= toDate)
            .Select(t => new TransactionSnapshot(t.Date, t.Amount, t.AccountId, t.CreditCardId, t.CategoryId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetAccountTotalsAsync(DateOnly? untilDate, CancellationToken cancellationToken)
    {
        var query = db.Transactions.AsNoTracking().Where(t => t.AccountId != null);
        if (untilDate is { } until)
        {
            query = query.Where(t => t.Date <= until);
        }

        return await query
            .GroupBy(t => t.AccountId!.Value)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total, cancellationToken);
    }

    public Task<decimal> SumOutflowsAsync(TransactionOwner owner, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var query = owner.Type == TransactionOwnerType.Account
            ? db.Transactions.Where(t => t.AccountId == owner.Id)
            : db.Transactions.Where(t => t.CreditCardId == owner.Id);

        return query
            .Where(t => t.Date >= fromDate && t.Date <= toDate && t.Amount < 0)
            .SumAsync(t => t.Amount, cancellationToken);
    }

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
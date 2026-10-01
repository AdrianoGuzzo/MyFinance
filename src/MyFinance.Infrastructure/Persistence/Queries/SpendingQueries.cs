using Microsoft.EntityFrameworkCore;

using MyFinance.Application.Analysis;
using MyFinance.Domain.Analysis;

namespace MyFinance.Infrastructure.Persistence.Queries;

internal sealed class SpendingQueries(FinanceDbContext db) : ISpendingQueries
{
    public async Task<IReadOnlyList<SpendingEntry>> GetEntriesAsync(
        DateOnly fromMonth, DateOnly toMonth, Guid? creditCardId, CancellationToken cancellationToken)
    {
        var query =
            from t in db.Transactions.AsNoTracking()
            join i in db.Invoices on t.InvoiceId equals i.Id
            where i.ReferenceMonth >= fromMonth && i.ReferenceMonth <= toMonth
            select new { t, i.ReferenceMonth };

        if (creditCardId is { } cardId)
        {
            query = query.Where(x => x.t.CreditCardId == cardId);
        }

        return await query
            .Select(x => new SpendingEntry(
                x.t.Id, x.t.CreditCardId, x.t.InvoiceId, x.ReferenceMonth, x.t.Date, x.t.Amount, x.t.Kind,
                x.t.CategoryId, x.t.MerchantKey, x.t.MerchantName, x.t.InstallmentPurchaseId, x.t.InstallmentNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<DateOnly?> GetFirstMonthAsync(CancellationToken cancellationToken) =>
        await (
            from t in db.Transactions
            join i in db.Invoices on t.InvoiceId equals i.Id
            orderby i.ReferenceMonth
            select (DateOnly?)i.ReferenceMonth)
        .FirstOrDefaultAsync(cancellationToken);
}
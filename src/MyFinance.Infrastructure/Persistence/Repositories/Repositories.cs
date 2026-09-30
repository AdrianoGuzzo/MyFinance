using Microsoft.EntityFrameworkCore;

using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Infrastructure.Persistence.Repositories;

internal sealed class CreditCardRepository(FinanceDbContext db) : ICreditCardRepository
{
    public Task<CreditCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.CreditCards.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CreditCard>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await db.CreditCards
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public void Add(CreditCard creditCard) => db.CreditCards.Add(creditCard);
}

internal sealed class InvoiceRepository(FinanceDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> ListByMonthsAsync(
        Guid creditCardId, IReadOnlyCollection<DateOnly> referenceMonths, CancellationToken cancellationToken) =>
        await db.Invoices
            .Where(i => i.CreditCardId == creditCardId && referenceMonths.Contains(i.ReferenceMonth))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid? creditCardId, CancellationToken cancellationToken) =>
        await db.Invoices
            .Where(i => creditCardId == null || i.CreditCardId == creditCardId)
            .OrderByDescending(i => i.ReferenceMonth)
            .ToListAsync(cancellationToken);

    public void Add(Invoice invoice) => db.Invoices.Add(invoice);
}

internal sealed class CategoryRepository(FinanceDbContext db) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await db.Categories
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken) => db.Categories.AnyAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(string name, Guid? parentCategoryId, Guid? exceptId, CancellationToken cancellationToken)
    {
        // Comparação em memória: o NOCASE do SQLite só trata ASCII. Aqui "Saúde", "SAUDE" e "saude" são o mesmo nome.
        var normalized = TransactionFingerprint.NormalizeDescription(name);
        var siblings = await db.Categories
            .Where(c => c.ParentCategoryId == parentCategoryId && c.Id != exceptId)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        return siblings.Any(n => string.Equals(TransactionFingerprint.NormalizeDescription(n), normalized, StringComparison.Ordinal));
    }

    public void Add(Category category) => db.Categories.Add(category);

    public void AddRange(IEnumerable<Category> categories) => db.Categories.AddRange(categories);
}

internal sealed class CategoryRuleRepository(FinanceDbContext db) : ICategoryRuleRepository
{
    public Task<CategoryRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.CategoryRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CategoryRule>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await db.CategoryRules
            .Where(r => includeInactive || r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Pattern)
            .ToListAsync(cancellationToken);

    public void Add(CategoryRule rule) => db.CategoryRules.Add(rule);

    public void AddRange(IEnumerable<CategoryRule> rules) => db.CategoryRules.AddRange(rules);

    public void Remove(CategoryRule rule) => db.CategoryRules.Remove(rule);
}

internal sealed class TransactionRepository(FinanceDbContext db) : ITransactionRepository
{
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ExistingTransaction>> GetForDuplicateCheckAsync(
        Guid creditCardId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken) =>
        await db.Transactions
            .Where(t => t.CreditCardId == creditCardId)
            .Where(t => (t.Date >= fromDate && t.Date <= toDate)
                || (t.ExternalId != null && externalIds.Contains(t.ExternalId)))
            .AsNoTracking()
            .Select(t => new ExistingTransaction(t.Id, t.Date, t.Amount, t.Description, t.ExternalId, t.ImportHash))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> ListUncategorizedAsync(CancellationToken cancellationToken) =>
        await db.Transactions
            .Where(t => t.CategoryId == null && t.Kind != TransactionKind.Payment)
            .ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<Transaction> transactions) => db.Transactions.AddRange(transactions);
}

internal sealed class InstallmentPurchaseRepository(FinanceDbContext db) : IInstallmentPurchaseRepository
{
    public Task<InstallmentPurchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.InstallmentPurchases.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InstallmentPurchase>> ListByMerchantsAsync(
        Guid creditCardId, IReadOnlyCollection<string> merchantKeys, CancellationToken cancellationToken) =>
        await db.InstallmentPurchases
            .Where(p => p.CreditCardId == creditCardId && merchantKeys.Contains(p.MerchantKey))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InstallmentPurchase>> ListAsync(CancellationToken cancellationToken) =>
        await db.InstallmentPurchases.AsNoTracking().OrderBy(p => p.FirstInvoiceMonth).ToListAsync(cancellationToken);

    public void Add(InstallmentPurchase purchase) => db.InstallmentPurchases.Add(purchase);
}

internal sealed class SpendingLimitRepository(FinanceDbContext db) : ISpendingLimitRepository
{
    public Task<SpendingLimit?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.SpendingLimits.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<SpendingLimit?> FindByCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        db.SpendingLimits.FirstOrDefaultAsync(l => l.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyList<SpendingLimit>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await db.SpendingLimits.Where(l => includeInactive || l.IsActive).ToListAsync(cancellationToken);

    public void Add(SpendingLimit limit) => db.SpendingLimits.Add(limit);

    public void Remove(SpendingLimit limit) => db.SpendingLimits.Remove(limit);
}

internal sealed class FinancialGoalRepository(FinanceDbContext db) : IFinancialGoalRepository
{
    public Task<FinancialGoal?> GetActiveAsync(CancellationToken cancellationToken) =>
        db.FinancialGoals
            .Where(g => g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(FinancialGoal goal) => db.FinancialGoals.Add(goal);
}

internal sealed class RecurringExpenseRepository(FinanceDbContext db) : IRecurringExpenseRepository
{
    public Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecurringExpense>> ListAsync(CancellationToken cancellationToken) =>
        await db.RecurringExpenses.ToListAsync(cancellationToken);

    public void Add(RecurringExpense expense) => db.RecurringExpenses.Add(expense);
}

internal sealed class ImportRepository(FinanceDbContext db) : IImportRepository
{
    public Task<Import?> FindLatestCompletedByFileHashAsync(Sha256Hash fileHash, CancellationToken cancellationToken) =>
        db.Imports
            .AsNoTracking()
            .Where(i => i.FileHash == fileHash && i.Status == ImportStatus.Completed)
            .OrderByDescending(i => i.ImportedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(Import import) => db.Imports.Add(import);
}
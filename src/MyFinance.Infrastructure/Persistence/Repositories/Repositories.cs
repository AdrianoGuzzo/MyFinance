using Microsoft.EntityFrameworkCore;

using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(FinanceDbContext db) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Account>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await db.Accounts
            .Where(a => includeInactive || a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public void Add(Account account) => db.Accounts.Add(account);
}

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

internal sealed class TransactionRepository(FinanceDbContext db) : ITransactionRepository
{
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ExistingTransaction>> GetForDuplicateCheckAsync(
        TransactionOwner owner,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken)
    {
        var query = owner.Type == TransactionOwnerType.Account
            ? db.Transactions.Where(t => t.AccountId == owner.Id)
            : db.Transactions.Where(t => t.CreditCardId == owner.Id);

        return await query
            .Where(t => (t.Date >= fromDate && t.Date <= toDate)
                || (t.ExternalId != null && externalIds.Contains(t.ExternalId)))
            .AsNoTracking()
            .Select(t => new ExistingTransaction(t.Id, t.Date, t.Amount, t.Description, t.ExternalId, t.ImportHash))
            .ToListAsync(cancellationToken);
    }

    public void AddRange(IEnumerable<Transaction> transactions) => db.Transactions.AddRange(transactions);
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
using MyFinance.Domain.Entities;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Interfaces;

/// <summary>Parcela <paramref name="Number"/> já vinculada à compra parcelada <paramref name="PurchaseId"/>.</summary>
public sealed record InstallmentLink(Guid PurchaseId, int Number);

public interface ICreditCardRepository
{
    Task<CreditCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditCard>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    void Add(CreditCard creditCard);
}

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Faturas do cartão nos meses de referência informados (as que existirem).</summary>
    Task<IReadOnlyList<Invoice>> ListByMonthsAsync(Guid creditCardId, IReadOnlyCollection<DateOnly> referenceMonths, CancellationToken cancellationToken);

    /// <summary>Faturas (de um cartão ou de todos) ordenadas por mês de referência decrescente.</summary>
    Task<IReadOnlyList<Invoice>> ListAsync(Guid? creditCardId, CancellationToken cancellationToken);

    void Add(Invoice invoice);
}

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <summary>Verifica nome já usado entre categorias irmãs (mesmo pai), ignorando maiúsculas/minúsculas e acentos.</summary>
    Task<bool> NameExistsAsync(string name, Guid? parentCategoryId, Guid? exceptId, CancellationToken cancellationToken);

    void Add(Category category);

    void AddRange(IEnumerable<Category> categories);
}

public interface ICategoryRuleRepository
{
    Task<CategoryRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryRule>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    void Add(CategoryRule rule);

    void AddRange(IEnumerable<CategoryRule> rules);

    void Remove(CategoryRule rule);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lançamentos com os ids informados (os que existirem), rastreados para alteração.</summary>
    Task<IReadOnlyList<Transaction>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Lançamentos do cartão que podem ser duplicados dos itens de um arquivo:
    /// os do intervalo de datas do arquivo e os que possuem algum dos <paramref name="externalIds"/>.
    /// </summary>
    Task<IReadOnlyList<ExistingTransaction>> GetForDuplicateCheckAsync(
        Guid creditCardId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken);

    /// <summary>Parcelas já vinculadas às compras parceladas informadas.</summary>
    Task<IReadOnlyList<InstallmentLink>> GetInstallmentLinksAsync(IReadOnlyCollection<Guid> purchaseIds, CancellationToken cancellationToken);

    /// <summary>Lançamentos sem categoria (exceto pagamentos de fatura), rastreados para alteração.</summary>
    Task<IReadOnlyList<Transaction>> ListUncategorizedAsync(CancellationToken cancellationToken);

    void AddRange(IEnumerable<Transaction> transactions);
}

public interface IInstallmentPurchaseRepository
{
    Task<InstallmentPurchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Compras parceladas do cartão com os estabelecimentos informados.</summary>
    Task<IReadOnlyList<InstallmentPurchase>> ListByMerchantsAsync(Guid creditCardId, IReadOnlyCollection<string> merchantKeys, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstallmentPurchase>> ListAsync(CancellationToken cancellationToken);

    void Add(InstallmentPurchase purchase);
}

public interface ISpendingLimitRepository
{
    Task<SpendingLimit?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SpendingLimit?> FindByCategoryAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SpendingLimit>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    void Add(SpendingLimit limit);

    void Remove(SpendingLimit limit);
}

public interface IFinancialGoalRepository
{
    Task<FinancialGoal?> GetActiveAsync(CancellationToken cancellationToken);

    void Add(FinancialGoal goal);
}

public interface IRecurringExpenseRepository
{
    Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecurringExpense>> ListAsync(CancellationToken cancellationToken);

    void Add(RecurringExpense expense);
}

public interface IImportRepository
{
    /// <summary>Importação concluída mais recente do arquivo com este hash, se houver.</summary>
    Task<Import?> FindLatestCompletedByFileHashAsync(Sha256Hash fileHash, CancellationToken cancellationToken);

    void Add(Import import);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
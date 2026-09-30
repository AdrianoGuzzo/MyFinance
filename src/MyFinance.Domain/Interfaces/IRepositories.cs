using MyFinance.Domain.Entities;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Account>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    void Add(Account account);
}

public interface ICreditCardRepository
{
    Task<CreditCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditCard>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    void Add(CreditCard creditCard);
}

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <summary>Verifica nome já usado entre categorias irmãs (mesmo pai), ignorando maiúsculas/minúsculas.</summary>
    Task<bool> NameExistsAsync(string name, Guid? parentCategoryId, Guid? exceptId, CancellationToken cancellationToken);

    void Add(Category category);

    void AddRange(IEnumerable<Category> categories);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lançamentos da conta/cartão que podem ser duplicados dos itens de um arquivo:
    /// os do intervalo de datas do arquivo e os que possuem algum dos <paramref name="externalIds"/>.
    /// </summary>
    Task<IReadOnlyList<ExistingTransaction>> GetForDuplicateCheckAsync(
        TransactionOwner owner,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<Transaction> transactions);
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
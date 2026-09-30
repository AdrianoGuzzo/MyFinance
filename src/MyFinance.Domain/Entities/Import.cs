using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Uma importação de arquivo (OFX, CSV...) para uma conta ou cartão.
/// Agrega os itens lidos (<see cref="ImportTransaction"/>) e cria os lançamentos ao ser concluída.
/// </summary>
public sealed class Import
{
    public const int FileNameMaxLength = 255;

    private readonly List<ImportTransaction> _transactions = [];

    private Import() { } // EF Core

    public Guid Id { get; private set; }

    /// <summary>Somente o nome do arquivo, nunca o caminho completo.</summary>
    public string FileName { get; private set; } = string.Empty;

    public Sha256Hash FileHash { get; private set; } = null!;

    public ImportFileType FileType { get; private set; }

    public Guid? AccountId { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public DateTime ImportedAt { get; private set; }

    /// <summary>Total de transações encontradas no arquivo (novas + duplicadas + com erro).</summary>
    public int TransactionCount { get; private set; }

    public ImportStatus Status { get; private set; }

    public IReadOnlyCollection<ImportTransaction> Transactions => _transactions.AsReadOnly();

    public TransactionOwner Owner => TransactionOwner.From(AccountId, CreditCardId);

    public ImportSummary Summary => new(
        Total: _transactions.Count,
        New: _transactions.Count(t => t.Status is ImportTransactionStatus.New or ImportTransactionStatus.Imported),
        Duplicates: _transactions.Count(t => t.Status == ImportTransactionStatus.Duplicate),
        Errors: _transactions.Count(t => t.Status == ImportTransactionStatus.Invalid));

    public static Import Start(
        string fileName,
        Sha256Hash fileHash,
        ImportFileType fileType,
        TransactionOwner owner,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(fileHash);

        return new Import
        {
            Id = Guid.CreateVersion7(),
            FileName = Guard.Required(Path.GetFileName(fileName), FileNameMaxLength, "O nome do arquivo"),
            FileHash = fileHash,
            FileType = Guard.Defined(fileType, "Tipo de arquivo"),
            AccountId = owner.AccountId,
            CreditCardId = owner.CreditCardId,
            ImportedAt = Guard.Utc(nowUtc),
            Status = ImportStatus.Pending,
        };
    }

    public ImportTransaction AddEntry(
        DateOnly date,
        decimal amount,
        string description,
        string? externalId,
        Sha256Hash importHash,
        string? rawData,
        DuplicateCheck duplicateCheck)
    {
        EnsurePending();
        var entry = ImportTransaction.Valid(Id, date, amount, description, externalId, importHash, rawData, duplicateCheck);
        _transactions.Add(entry);
        TransactionCount = _transactions.Count;
        return entry;
    }

    public ImportTransaction AddInvalidEntry(
        string errorMessage,
        string? rawData,
        DateOnly? date = null,
        decimal? amount = null,
        string? description = null,
        string? externalId = null)
    {
        EnsurePending();
        var entry = ImportTransaction.Invalid(Id, errorMessage, rawData, date, amount, description, externalId);
        _transactions.Add(entry);
        TransactionCount = _transactions.Count;
        return entry;
    }

    /// <summary>
    /// Conclui a importação criando um lançamento para cada item novo.
    /// Duplicados e inválidos são mantidos apenas como histórico.
    /// </summary>
    public IReadOnlyList<Transaction> Complete(DateTime nowUtc)
    {
        EnsurePending();

        var created = new List<Transaction>();
        foreach (var entry in _transactions.Where(t => t.Status == ImportTransactionStatus.New))
        {
            var transaction = Transaction.Create(
                Owner,
                entry.Date!.Value,
                entry.Amount!.Value,
                entry.Description!,
                nowUtc,
                entry.ExternalId,
                entry.ImportHash);

            entry.MarkImported(transaction.Id);
            created.Add(transaction);
        }

        Status = ImportStatus.Completed;
        return created;
    }

    public void Cancel()
    {
        EnsurePending();
        Status = ImportStatus.Cancelled;
    }

    public void Fail()
    {
        EnsurePending();
        Status = ImportStatus.Failed;
    }

    private void EnsurePending()
    {
        if (Status != ImportStatus.Pending)
        {
            throw new DomainException("Esta importação já foi finalizada.");
        }
    }
}
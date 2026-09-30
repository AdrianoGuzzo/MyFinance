using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Item lido de um arquivo de importação e o que aconteceu com ele.
/// <see cref="TransactionId"/> aponta para o lançamento criado (status Imported)
/// ou para o lançamento já existente que causou a duplicidade (status Duplicate).
/// </summary>
public sealed class ImportTransaction
{
    public const int ErrorMessageMaxLength = 500;

    private ImportTransaction() { } // EF Core

    public Guid Id { get; private set; }

    public Guid ImportId { get; private set; }

    public string? ExternalId { get; private set; }

    public DateOnly? Date { get; private set; }

    public decimal? Amount { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Conteúdo original do registro no arquivo (armazenado apenas localmente; nunca em logs).</summary>
    public string? RawData { get; private set; }

    public ImportTransactionStatus Status { get; private set; }

    public DuplicateReason DuplicateReason { get; private set; }

    public Sha256Hash? ImportHash { get; private set; }

    public string? ErrorMessage { get; private set; }

    public Guid? TransactionId { get; private set; }

    internal static ImportTransaction Valid(
        Guid importId,
        DateOnly date,
        decimal amount,
        string description,
        string? externalId,
        Sha256Hash importHash,
        string? rawData,
        DuplicateCheck duplicateCheck)
    {
        ArgumentNullException.ThrowIfNull(importHash);
        ArgumentNullException.ThrowIfNull(duplicateCheck);

        return new ImportTransaction
        {
            Id = Guid.CreateVersion7(),
            ImportId = importId,
            Date = date,
            Amount = amount,
            Description = description,
            ExternalId = externalId,
            ImportHash = importHash,
            RawData = rawData,
            Status = duplicateCheck.IsDuplicate ? ImportTransactionStatus.Duplicate : ImportTransactionStatus.New,
            DuplicateReason = duplicateCheck.Reason,
            TransactionId = duplicateCheck.ExistingTransactionId,
        };
    }

    internal static ImportTransaction Invalid(
        Guid importId,
        string errorMessage,
        string? rawData,
        DateOnly? date,
        decimal? amount,
        string? description,
        string? externalId) => new()
        {
            Id = Guid.CreateVersion7(),
            ImportId = importId,
            ErrorMessage = Truncate(Guard.Required(errorMessage, int.MaxValue, "A mensagem de erro"), ErrorMessageMaxLength),
            RawData = rawData,
            Date = date,
            Amount = amount,
            Description = description,
            ExternalId = externalId,
            Status = ImportTransactionStatus.Invalid,
        };

    /// <summary>A mensagem é só histórico: nunca deve impedir a importação das demais linhas.</summary>
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");

    internal void MarkImported(Guid transactionId)
    {
        Status = ImportTransactionStatus.Imported;
        TransactionId = transactionId;
    }
}
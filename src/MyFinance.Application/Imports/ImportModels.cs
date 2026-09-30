using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Imports;

/// <summary>"Este arquivo já foi importado. Data da importação: 30/09/2026. Transações: 127".</summary>
public sealed record PreviousImportInfo(DateTime ImportedAtUtc, int TransactionCount, string FileName);

/// <summary>Resultado dos passos "Selecionar arquivo", "Identificar formato" e "Ler arquivo".</summary>
public sealed class ImportFileAnalysis
{
    internal ImportFileAnalysis(string fileName, Sha256Hash fileHash, ImportResult result)
    {
        FileName = fileName;
        FileHash = fileHash;
        Result = result;
    }

    public string FileName { get; }

    public Sha256Hash FileHash { get; }

    public ImportFileType FileType => Result.FileType;

    public StatementKind StatementKind => Result.StatementKind;

    public int TotalFound => Result.TotalFound;

    /// <summary>Preenchido quando um arquivo idêntico (mesmo SHA-256) já foi importado.</summary>
    public PreviousImportInfo? PreviousImport { get; internal init; }

    /// <summary>Conta/cartão sugerido a partir do identificador presente no arquivo.</summary>
    public TransactionOwner? SuggestedOwner { get; internal init; }

    internal ImportResult Result { get; }
}

/// <summary>Linha da prévia: Data | Descrição | Valor | Status.</summary>
public sealed record ImportPreviewRow(
    int Index,
    DateOnly? Date,
    string Description,
    decimal? Amount,
    ImportTransactionStatus Status,
    DuplicateReason DuplicateReason,
    string? Message)
{
    internal string? ExternalId { get; init; }

    internal string? RawData { get; init; }

    internal Sha256Hash? ImportHash { get; init; }

    internal Guid? ExistingTransactionId { get; init; }
}

public sealed class ImportPreview
{
    internal ImportPreview(
        ImportFileAnalysis analysis,
        TransactionOwner owner,
        string ownerName,
        bool amountsInverted,
        bool inversionSuggested,
        IReadOnlyList<ImportPreviewRow> rows)
    {
        Analysis = analysis;
        Owner = owner;
        OwnerName = ownerName;
        AmountsInverted = amountsInverted;
        InversionSuggested = inversionSuggested;
        Rows = rows;
        Summary = new ImportSummary(
            rows.Count,
            rows.Count(r => r.Status == ImportTransactionStatus.New),
            rows.Count(r => r.Status == ImportTransactionStatus.Duplicate),
            rows.Count(r => r.Status == ImportTransactionStatus.Invalid));
    }

    public ImportFileAnalysis Analysis { get; }

    public TransactionOwner Owner { get; }

    public string OwnerName { get; }

    /// <summary>Os sinais dos valores do arquivo foram invertidos nesta prévia.</summary>
    public bool AmountsInverted { get; }

    /// <summary>
    /// A aplicação recomenda inverter os sinais: destino é um cartão, arquivo CSV e a maioria dos valores é positiva
    /// (faturas em CSV costumam trazer compras como valores positivos).
    /// </summary>
    public bool InversionSuggested { get; }

    public IReadOnlyList<ImportPreviewRow> Rows { get; }

    public ImportSummary Summary { get; }

    public bool IsConfirmed { get; internal set; }
}

public sealed record ImportOutcome(Guid ImportId, ImportSummary Summary);
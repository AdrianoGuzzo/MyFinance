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

    /// <summary>Data mais antiga entre as transações lidas (para sugerir faturas).</summary>
    public DateOnly? FirstDate => Result.Transactions.Count == 0 ? null : Result.Transactions.Min(t => t.Date);

    /// <summary>Data mais recente entre as transações lidas.</summary>
    public DateOnly? LastDate => Result.Transactions.Count == 0 ? null : Result.Transactions.Max(t => t.Date);

    /// <summary>Preenchido quando um arquivo idêntico (mesmo SHA-256) já foi importado.</summary>
    public PreviousImportInfo? PreviousImport { get; internal init; }

    /// <summary>Cartão sugerido pelos 4 últimos dígitos presentes no arquivo.</summary>
    public Guid? SuggestedCreditCardId { get; internal init; }

    internal ImportResult Result { get; }
}

/// <summary>Linha da prévia: Data | Estabelecimento | Parcela | Fatura | Tipo | Valor | Status.</summary>
/// <param name="InvoiceMonth">Mês de referência da fatura em que o lançamento entrará (itens válidos).</param>
/// <param name="Kind">Tipo sugerido (itens válidos).</param>
/// <param name="MerchantName">Estabelecimento identificado na descrição (itens válidos).</param>
/// <param name="InstallmentNumber">Parcela identificada na descrição ("3/12" → 3).</param>
/// <param name="InstallmentCount">Total de parcelas ("3/12" → 12).</param>
public sealed record ImportPreviewRow(
    int Index,
    DateOnly? Date,
    string Description,
    decimal? Amount,
    ImportTransactionStatus Status,
    DuplicateReason DuplicateReason,
    string? Message,
    DateOnly? InvoiceMonth = null,
    TransactionKind? Kind = null,
    string? MerchantName = null,
    int? InstallmentNumber = null,
    int? InstallmentCount = null)
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
        Guid creditCardId,
        string creditCardName,
        bool amountsInverted,
        bool inversionSuggested,
        DateOnly? invoiceMonth,
        IReadOnlyList<ImportPreviewRow> rows)
    {
        InvoiceMonth = invoiceMonth;
        Analysis = analysis;
        CreditCardId = creditCardId;
        CreditCardName = creditCardName;
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

    public Guid CreditCardId { get; }

    public string CreditCardName { get; }

    /// <summary>Os sinais dos valores do arquivo foram invertidos nesta prévia.</summary>
    public bool AmountsInverted { get; }

    /// <summary>
    /// A aplicação recomenda inverter os sinais: arquivo CSV e a maioria dos valores é positiva
    /// (faturas em CSV costumam trazer compras como valores positivos).
    /// </summary>
    public bool InversionSuggested { get; }

    /// <summary>Fatura escolhida pelo usuário para o arquivo inteiro; <c>null</c> = definida pela data de cada lançamento.</summary>
    public DateOnly? InvoiceMonth { get; }

    public IReadOnlyList<ImportPreviewRow> Rows { get; }

    public ImportSummary Summary { get; }

    public bool IsConfirmed { get; internal set; }
}

public sealed record ImportOutcome(Guid ImportId, ImportSummary Summary);
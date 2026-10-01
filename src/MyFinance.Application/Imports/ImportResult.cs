using MyFinance.Domain.Enums;

namespace MyFinance.Application.Imports;

/// <summary>Tipo de extrato identificado no arquivo. Só extratos de cartão (ou desconhecidos, como CSV) são importados.</summary>
public enum StatementKind
{
    Unknown = 0,
    BankAccount = 1,
    CreditCard = 2,
}

/// <summary>
/// Registro do arquivo que não pôde ser convertido.
/// <paramref name="Position"/> é o número da linha (CSV) ou a ordem da transação no arquivo (OFX).
/// </summary>
public sealed record ImportRowError(int Position, string Message, string? RawData);

/// <summary>Resultado da leitura de um arquivo: transações válidas e registros com erro.</summary>
public sealed class ImportResult
{
    public required ImportFileType FileType { get; init; }

    public IReadOnlyList<ImportedTransaction> Transactions { get; init; } = [];

    public IReadOnlyList<ImportRowError> Errors { get; init; } = [];

    public StatementKind StatementKind { get; init; } = StatementKind.Unknown;

    /// <summary>
    /// Identificador do cartão informado no arquivo (ex.: ACCTID do OFX), para sugerir o cartão de destino.
    /// Dado sensível: nunca registrar em log.
    /// </summary>
    public string? StatementAccountId { get; init; }

    public int TotalFound => Transactions.Count + Errors.Count;
}
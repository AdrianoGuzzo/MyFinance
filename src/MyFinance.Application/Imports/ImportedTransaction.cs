namespace MyFinance.Application.Imports;

/// <summary>
/// Transação lida de um arquivo, já convertida para um modelo neutro (independente de OFX, CSV ou banco).
/// A aplicação valida e converte este modelo em entidades do domínio.
/// </summary>
public sealed class ImportedTransaction
{
    /// <summary>Data do lançamento (sem horário/fuso — ver ADR 0003).</summary>
    public DateOnly Date { get; init; }

    /// <summary>Valor com o sinal como aparece no arquivo (negativo = saída).</summary>
    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>Identificador fornecido pelo banco (FITID no OFX), quando houver.</summary>
    public string? ExternalId { get; init; }

    /// <summary>Saldo após a transação, quando o arquivo informa.</summary>
    public decimal? Balance { get; init; }

    /// <summary>Conteúdo original do registro. Nunca deve ser registrado em log.</summary>
    public string? RawData { get; init; }
}
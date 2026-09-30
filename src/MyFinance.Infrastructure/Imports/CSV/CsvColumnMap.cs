using MyFinance.Domain.Services;

namespace MyFinance.Infrastructure.Imports.CSV;

/// <summary>
/// Posição das colunas relevantes, identificadas pelo cabeçalho (sem diferenciar maiúsculas nem acentos).
/// Os nomes cobrem exportações comuns de bancos brasileiros e em inglês, sem regras específicas por banco.
/// </summary>
internal sealed record CsvColumnMap(int Date, int Amount, int Description, int? ExternalId, int? Balance, int HeaderFieldCount)
{
    private static readonly string[] DateNames =
        ["DATA", "DATE", "DT", "DATA LANCAMENTO", "DATA DE LANCAMENTO", "DATA DO LANCAMENTO", "DATA DA TRANSACAO", "DATA MOVIMENTO", "TRANSACTION DATE"];

    private static readonly string[] AmountNames =
        ["VALOR", "AMOUNT", "VALUE", "VALOR (R$)", "VALOR R$", "QUANTIA"];

    private static readonly string[] DescriptionNames =
        ["DESCRICAO", "DESCRIPTION", "TITLE", "HISTORICO", "LANCAMENTO", "ESTABELECIMENTO", "MEMO", "DETALHES", "DETALHE"];

    private static readonly string[] ExternalIdNames =
        ["IDENTIFICADOR", "ID", "FITID", "CODIGO", "TRANSACTION ID", "ID DA TRANSACAO"];

    private static readonly string[] BalanceNames =
        ["SALDO", "BALANCE", "SALDO (R$)", "SALDO R$"];

    public int RequiredFieldCount => new[] { Date, Amount, Description }.Max() + 1;

    public static CsvColumnMap? FromHeader(IReadOnlyList<string> header)
    {
        var normalized = header.Select(TransactionFingerprint.NormalizeDescription).ToList();

        int? Find(string[] names)
        {
            var index = normalized.FindIndex(h => names.Contains(h, StringComparer.Ordinal));
            return index >= 0 ? index : null;
        }

        var date = Find(DateNames);
        var amount = Find(AmountNames);
        var description = Find(DescriptionNames);

        return date is null || amount is null || description is null
            ? null
            : new CsvColumnMap(date.Value, amount.Value, description.Value, Find(ExternalIdNames), Find(BalanceNames), header.Count);
    }
}
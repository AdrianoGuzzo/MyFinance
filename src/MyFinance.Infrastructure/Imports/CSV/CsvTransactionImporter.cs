using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;
using MyFinance.Domain.Enums;
using MyFinance.Infrastructure.Imports.Common;

namespace MyFinance.Infrastructure.Imports.CSV;

/// <summary>
/// Importa extratos CSV genéricos. Requer cabeçalho com colunas de data, valor e descrição;
/// identificador e saldo são opcionais. O delimitador (vírgula, ponto e vírgula ou tab) é detectado.
/// Os valores são mantidos com o sinal do arquivo — a aplicação decide se precisa invertê-los
/// (faturas de cartão em CSV costumam trazer compras como valores positivos).
/// </summary>
public sealed class CsvTransactionImporter : ITransactionImporter
{
    public bool CanHandle(string fileName) =>
        Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<ImportResult> ImportAsync(Stream stream, CancellationToken cancellationToken)
    {
        var text = await ImportText.ReadAllTextAsync(stream, cancellationToken).ConfigureAwait(false);
        var records = CsvReader.Read(text, CsvReader.DetectDelimiter(text)).Where(r => !r.IsBlank);

        CsvColumnMap? columns = null;
        var transactions = new List<ImportedTransaction>();
        var errors = new List<ImportRowError>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (columns is null)
            {
                columns = CsvColumnMap.FromHeader(record.Fields) ?? throw new ImportException(
                    "Não foi possível identificar as colunas do CSV. A primeira linha deve ser um cabeçalho com colunas de data, valor e descrição.");
                continue;
            }

            var (transaction, error) = Convert(record, columns);
            if (transaction is not null)
            {
                transactions.Add(transaction);
            }
            else
            {
                errors.Add(error!);
            }
        }

        return new ImportResult
        {
            FileType = ImportFileType.Csv,
            Transactions = transactions,
            Errors = errors,
        };
    }

    private static (ImportedTransaction? Transaction, ImportRowError? Error) Convert(CsvRecord record, CsvColumnMap columns)
    {
        ImportRowError Fail(string message) => new(record.LineNumber, message, record.RawText);

        if (record.Fields.Count < columns.RequiredFieldCount)
        {
            return (null, Fail($"Linha com {record.Fields.Count} coluna(s); esperado ao menos {columns.RequiredFieldCount}."));
        }

        // Colunas a mais (ex.: vírgula decimal sem aspas) deslocariam valores para os campos errados.
        var fieldCount = record.Fields.Count;
        while (fieldCount > columns.HeaderFieldCount && string.IsNullOrWhiteSpace(record.Fields[fieldCount - 1]))
        {
            fieldCount--;
        }

        if (fieldCount > columns.HeaderFieldCount)
        {
            return (null, Fail($"Linha com {fieldCount} coluna(s); o cabeçalho tem {columns.HeaderFieldCount}."));
        }

        var dateText = record.Fields[columns.Date];
        if (!DateParser.TryParseCsv(dateText, out var date))
        {
            return (null, Fail($"Data inválida: {ImportMessages.Quote(dateText)}."));
        }

        var amountText = record.Fields[columns.Amount];
        if (!AmountParser.TryParse(amountText, out var amount))
        {
            return (null, Fail($"Valor inválido: {ImportMessages.Quote(amountText)}."));
        }

        var description = record.Fields[columns.Description].Trim();
        if (description.Length == 0)
        {
            return (null, Fail("Transação sem descrição."));
        }

        var externalId = Optional(record, columns.ExternalId);
        var balanceText = Optional(record, columns.Balance);

        return (new ImportedTransaction
        {
            Date = date,
            Amount = amount,
            Description = description,
            ExternalId = externalId,
            Balance = AmountParser.TryParse(balanceText, out var balance) ? balance : null,
            RawData = record.RawText,
        }, null);
    }

    private static string? Optional(CsvRecord record, int? index)
    {
        if (index is not { } i || i >= record.Fields.Count)
        {
            return null;
        }

        var value = record.Fields[i].Trim();
        return value.Length == 0 ? null : value;
    }
}
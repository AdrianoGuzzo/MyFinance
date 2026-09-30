using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;
using MyFinance.Domain.Enums;
using MyFinance.Infrastructure.Imports.Common;

namespace MyFinance.Infrastructure.Imports.OFX;

/// <summary>
/// Importa extratos OFX (conta corrente e cartão de crédito) de qualquer banco.
/// </summary>
public sealed class OfxTransactionImporter : ITransactionImporter
{
    public bool CanHandle(string fileName) =>
        Path.GetExtension(fileName) is var ext
        && (ext.Equals(".ofx", StringComparison.OrdinalIgnoreCase) || ext.Equals(".qfx", StringComparison.OrdinalIgnoreCase));

    public async Task<ImportResult> ImportAsync(Stream stream, CancellationToken cancellationToken)
    {
        var text = await ImportText.ReadAllTextAsync(stream, cancellationToken).ConfigureAwait(false);

        var start = text.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            throw new ImportException("O arquivo não é um OFX válido: a marcação <OFX> não foi encontrada.");
        }

        var document = OfxDocument.Parse(text[start..]);

        // Um arquivo com conta e cartão (ou duas contas) não pode ser importado para um único destino.
        var statementCount = document.Descendants("STMTRS").Count() + document.Descendants("CCSTMTRS").Count();
        if (statementCount > 1)
        {
            throw new ImportException(
                $"O arquivo contém {statementCount} extratos (contas ou cartões diferentes). Exporte e importe um extrato por arquivo.");
        }
        var creditCardStatement = document.Descendants("CCSTMTRS").FirstOrDefault();
        var bankStatement = document.Descendants("STMTRS").FirstOrDefault();

        if (creditCardStatement is null && bankStatement is null)
        {
            throw new ImportException("O arquivo OFX não contém um extrato de conta ou de cartão de crédito.");
        }

        var transactions = new List<ImportedTransaction>();
        var errors = new List<ImportRowError>();
        var position = 0;

        foreach (var element in document.Descendants("STMTTRN"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            position++;

            var (transaction, error) = Convert(element, position);
            if (transaction is not null)
            {
                transactions.Add(transaction);
            }
            else
            {
                errors.Add(error!);
            }
        }

        var accountFrom = (creditCardStatement ?? bankStatement)!
            .Descendants(creditCardStatement is not null ? "CCACCTFROM" : "BANKACCTFROM")
            .FirstOrDefault();

        return new ImportResult
        {
            FileType = ImportFileType.Ofx,
            Transactions = transactions,
            Errors = errors,
            StatementKind = creditCardStatement is not null ? StatementKind.CreditCard : StatementKind.BankAccount,
            StatementAccountId = accountFrom?.FindValue("ACCTID"),
        };
    }

    private static (ImportedTransaction? Transaction, ImportRowError? Error) Convert(OfxElement element, int position)
    {
        var raw = string.Join(';', element.Leaves().Select(c => $"{c.Name}={c.Value}"));

        var postedAt = element.FindValue("DTPOSTED");
        if (!DateParser.TryParseOfx(postedAt, out var date))
        {
            return (null, new ImportRowError(position, $"Data inválida: {ImportMessages.Quote(postedAt)}.", raw));
        }

        var amountText = element.FindValue("TRNAMT");
        if (!AmountParser.TryParse(amountText, out var amount))
        {
            return (null, new ImportRowError(position, $"Valor inválido: {ImportMessages.Quote(amountText)}.", raw));
        }

        var description = BuildDescription(element.FindValue("NAME"), element.FindValue("MEMO"));
        if (description.Length == 0)
        {
            return (null, new ImportRowError(position, "Transação sem descrição.", raw));
        }

        var fitId = element.FindValue("FITID")?.Trim();

        return (new ImportedTransaction
        {
            Date = date,
            Amount = amount,
            Description = description,
            ExternalId = string.IsNullOrEmpty(fitId) ? null : fitId,
            RawData = raw,
        }, null);
    }

    /// <summary>
    /// NAME costuma ser curto (até 32 caracteres) e MEMO, detalhado. Se um contém o outro, usa o mais longo;
    /// se são diferentes, combina "NAME - MEMO".
    /// </summary>
    private static string BuildDescription(string? name, string? memo)
    {
        name = name?.Trim() ?? string.Empty;
        memo = memo?.Trim() ?? string.Empty;

        if (name.Length == 0 || memo.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            return memo;
        }

        if (memo.Length == 0 || name.Contains(memo, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"{name} - {memo}";
    }
}
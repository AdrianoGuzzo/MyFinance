using System.Diagnostics;

using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Common.Logging;
using MyFinance.Application.Invoices;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Imports;

/// <summary>
/// Caso de uso ImportTransactions, em três passos:
/// <list type="number">
/// <item><description><see cref="AnalyzeAsync"/> — identifica o formato, lê o arquivo, calcula o SHA-256 e sugere o cartão;</description></item>
/// <item><description><see cref="PreviewAsync"/> — valida com as regras do domínio, identifica fatura e tipo, detecta duplicidades (nada é gravado);</description></item>
/// <item><description><see cref="ConfirmAsync"/> — grava a importação, as faturas novas e os lançamentos novos em uma única transação.</description></item>
/// </list>
/// Cancelar = simplesmente não chamar <see cref="ConfirmAsync"/>.
/// </summary>
public sealed partial class ImportService(
    TransactionImporterResolver importerResolver,
    ICreditCardRepository creditCards,
    IInvoiceRepository invoices,
    ITransactionRepository transactions,
    IImportRepository imports,
    ICategoryRepository categories,
    ICategorizationService categorization,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ImportService> logger)
{
    /// <summary>Fatura fictícia usada só para validar itens com as regras do domínio antes de existir a fatura real.</summary>
    private static readonly Invoice ValidationInvoice = Invoice.For(
        CreditCard.Create("Validação", "Validação", CardBrand.Other, LastFourDigits.Create("0000"), 0m,
            DayOfMonth.Create(1), DayOfMonth.Create(10), DateTime.UnixEpoch),
        new InvoicePeriod(new DateOnly(2000, 1, 1), new DateOnly(2000, 2, 1), new DateOnly(2000, 2, 10)));

    public async Task<ImportFileAnalysis> AnalyzeAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var name = Path.GetFileName(fileName);
        var extension = Path.GetExtension(name);

        try
        {
            var importer = importerResolver.Resolve(name);
            using var content = await CopyToMemoryAsync(stream, cancellationToken);

            var hash = await Sha256Hash.ComputeAsync(content, cancellationToken);
            content.Position = 0;
            var hashPrefix = hash.Value[..8];
            LogImportStarted(logger, extension, hashPrefix);

            var result = await importer.ImportAsync(content, cancellationToken);
            if (result.StatementKind == StatementKind.BankAccount)
            {
                throw new ImportException(
                    "Este arquivo é um extrato de conta bancária. O MyFinance importa apenas faturas e extratos de cartão de crédito.");
            }

            var previous = await imports.FindLatestCompletedByFileHashAsync(hash, cancellationToken);

            return new ImportFileAnalysis(name, hash, result)
            {
                PreviousImport = previous is null ? null : new PreviousImportInfo(previous.ImportedAt, previous.TransactionCount, previous.FileName),
                SuggestedCreditCardId = await SuggestCreditCardAsync(result, cancellationToken),
            };
        }
        catch (ImportException ex)
        {
            LogImportRejected(logger, extension, ex.Message);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not AppException)
        {
            LogImportFailed(logger, ex);
            throw new ImportException("Não foi possível ler o arquivo.", ex);
        }
    }

    /// <param name="invertAmounts"><c>null</c> = automático (<see cref="ImportPreview.InversionSuggested"/>).</param>
    public async Task<ImportPreview> PreviewAsync(
        ImportFileAnalysis analysis,
        Guid creditCardId,
        bool? invertAmounts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var card = await GetActiveCardAsync(creditCardId, cancellationToken);
        var suggested = analysis.FileType == ImportFileType.Csv
            && analysis.Result.Transactions.Count(t => t.Amount > 0) > analysis.Result.Transactions.Count(t => t.Amount < 0);
        var invert = invertAmounts ?? suggested;

        var rows = await BuildRowsAsync(analysis.Result, card, invert, cancellationToken);
        return new ImportPreview(analysis, card.Id, card.Name, invert, suggested, rows);
    }

    public async Task<ImportOutcome> ConfirmAsync(ImportPreview preview, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (preview.IsConfirmed)
        {
            throw new ValidationException("Esta importação já foi confirmada.");
        }

        if (preview.Summary.Total == 0)
        {
            throw new ValidationException("O arquivo não contém transações para importar.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var card = await GetActiveCardAsync(preview.CreditCardId, cancellationToken);

            // Recalcula as duplicidades no momento da gravação: o banco pode ter mudado desde a prévia.
            var rows = await BuildRowsAsync(preview.Analysis.Result, card, preview.AmountsInverted, cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;

            var import = Import.Start(preview.Analysis.FileName, preview.Analysis.FileHash, preview.Analysis.FileType, card.Id, now);
            foreach (var row in rows)
            {
                if (row.Status == ImportTransactionStatus.Invalid)
                {
                    import.AddInvalidEntry(row.Message!, row.RawData, row.Date, row.Amount, row.Description, row.ExternalId);
                }
                else
                {
                    import.AddEntry(row.Date!.Value, row.Amount!.Value, row.Description, row.ExternalId, row.ImportHash!, row.RawData,
                        new DuplicateCheck(row.DuplicateReason, row.ExistingTransactionId), row.Kind!.Value, row.InvoiceMonth!.Value);
                }
            }

            var newMonths = rows.Where(r => r.Status == ImportTransactionStatus.New).Select(r => r.InvoiceMonth!.Value).ToList();
            var invoicesByMonth = await InvoiceBook.EnsureAsync(card, newMonths, invoices, cancellationToken);
            var created = import.Complete(invoicesByMonth, now);
            await ApplySuggestedCategoriesAsync(created, now, cancellationToken);

            imports.Add(import);
            transactions.AddRange(created);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            preview.IsConfirmed = true;
            var summary = import.Summary;
            LogImportCompleted(logger, import.Id, summary.Total, summary.New, summary.Duplicates, summary.Errors, stopwatch.ElapsedMilliseconds);
            return new ImportOutcome(import.Id, summary);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogImportFailed(logger, ex);
            throw;
        }
    }

    private async Task<IReadOnlyList<ImportPreviewRow>> BuildRowsAsync(
        ImportResult result,
        CreditCard card,
        bool invertAmounts,
        CancellationToken cancellationToken)
    {
        var rows = new List<ImportPreviewRow>();
        var valid = new List<(ImportPreviewRow Row, DuplicateCandidate Candidate)>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var index = 0;

        foreach (var imported in result.Transactions)
        {
            var amount = invertAmounts ? -imported.Amount : imported.Amount;
            var kind = TransactionKindClassifier.Classify(amount, imported.Description);
            var error = ValidateWithDomainRules(imported, amount, kind, now);

            if (error is not null)
            {
                rows.Add(new ImportPreviewRow(index++, imported.Date, imported.Description, amount, ImportTransactionStatus.Invalid, DuplicateReason.None, error)
                {
                    ExternalId = imported.ExternalId,
                    RawData = imported.RawData,
                });
                continue;
            }

            var hash = TransactionFingerprint.ComputeImportHash(imported.Date, amount, imported.Description, imported.Balance);
            var invoiceMonth = card.GetInvoicePeriod(imported.Date).ReferenceMonth;
            var row = new ImportPreviewRow(index++, imported.Date, imported.Description.Trim(), amount, ImportTransactionStatus.New, DuplicateReason.None, null,
                invoiceMonth, kind)
            {
                ExternalId = string.IsNullOrWhiteSpace(imported.ExternalId) ? null : imported.ExternalId.Trim(),
                RawData = imported.RawData,
                ImportHash = hash,
            };
            valid.Add((row, new DuplicateCandidate(imported.Date, amount, row.Description, row.ExternalId, hash)));
            rows.Add(row);
        }

        rows.AddRange(result.Errors.Select(e =>
            new ImportPreviewRow(index++, null, string.Empty, null, ImportTransactionStatus.Invalid, DuplicateReason.None,
                $"Registro {e.Position}: {e.Message}")
            { RawData = e.RawData }));

        if (valid.Count == 0)
        {
            return rows;
        }

        var existing = await transactions.GetForDuplicateCheckAsync(
            card.Id,
            valid.Min(v => v.Candidate.Date),
            valid.Max(v => v.Candidate.Date),
            [.. valid.Select(v => v.Candidate.ExternalId).OfType<string>().Distinct(StringComparer.Ordinal)],
            cancellationToken);

        var checks = DuplicateDetector.Detect([.. valid.Select(v => v.Candidate)], existing);

        for (var i = 0; i < valid.Count; i++)
        {
            if (!checks[i].IsDuplicate)
            {
                continue;
            }

            var row = valid[i].Row;
            rows[row.Index] = row with
            {
                Status = ImportTransactionStatus.Duplicate,
                DuplicateReason = checks[i].Reason,
                ExistingTransactionId = checks[i].ExistingTransactionId,
            };
        }

        return rows;
    }

    /// <summary>Aplica as mesmas regras que o domínio aplicará ao criar o lançamento (valor zero, casas decimais...).</summary>
    private static string? ValidateWithDomainRules(ImportedTransaction imported, decimal amount, TransactionKind kind, DateTime now)
    {
        try
        {
            _ = Transaction.Create(ValidationInvoice, imported.Date, amount, imported.Description, kind, now, imported.ExternalId);
            return null;
        }
        catch (DomainException ex)
        {
            return ex.Message;
        }
    }

    private async Task ApplySuggestedCategoriesAsync(IReadOnlyList<Transaction> created, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var transaction in created)
        {
            if (transaction.Kind == TransactionKind.Payment)
            {
                continue;
            }

            var input = new CategorizationInput(transaction.CreditCardId, transaction.Date, transaction.Amount, transaction.Description);
            if (await categorization.SuggestCategoryAsync(input, cancellationToken) is { } categoryId
                && await categories.GetByIdAsync(categoryId, cancellationToken) is { IsActive: true } category)
            {
                transaction.Categorize(category, now);
            }
        }
    }

    private async Task<Guid?> SuggestCreditCardAsync(ImportResult result, CancellationToken cancellationToken)
    {
        var fileDigits = Digits(result.StatementAccountId);
        if (fileDigits.Length < 4)
        {
            return null;
        }

        return (await creditCards.ListAsync(includeInactive: false, cancellationToken))
            .FirstOrDefault(c => c.LastFourDigits.Value == fileDigits[^4..])?.Id;
    }

    private async Task<CreditCard> GetActiveCardAsync(Guid creditCardId, CancellationToken cancellationToken)
    {
        var card = await creditCards.GetByIdAsync(creditCardId, cancellationToken);
        return card is { IsActive: true } ? card : throw new ValidationException("Selecione um cartão ativo.");
    }

    private static async Task<MemoryStream> CopyToMemoryAsync(Stream stream, CancellationToken cancellationToken)
    {
        var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > ImportLimits.MaxFileSizeBytes)
            {
                await memory.DisposeAsync();
                throw new ImportException($"O arquivo é grande demais (limite de {ImportLimits.MaxFileSizeBytes / 1024 / 1024} MB).");
            }

            memory.Write(buffer, 0, read);
        }

        memory.Position = 0;
        return memory;
    }

    private static string Digits(string? value) => new([.. (value ?? string.Empty).Where(char.IsAsciiDigit)]);

    // Logs nunca incluem nome do arquivo, descrições, valores ou números de conta — apenas metadados técnicos.
    [LoggerMessage(EventId = LogEvents.ImportStarted, EventName = nameof(LogEvents.ImportStarted), Level = LogLevel.Information,
        Message = "Importação iniciada: extensão {Extension}, hash {HashPrefix}")]
    private static partial void LogImportStarted(ILogger logger, string extension, string hashPrefix);

    [LoggerMessage(EventId = LogEvents.ImportCompleted, EventName = nameof(LogEvents.ImportCompleted), Level = LogLevel.Information,
        Message = "Importação {ImportId} concluída: {Total} encontradas, {New} novas, {Duplicates} duplicadas, {Errors} com erro em {ElapsedMs} ms")]
    private static partial void LogImportCompleted(ILogger logger, Guid importId, int total, int @new, int duplicates, int errors, long elapsedMs);

    [LoggerMessage(EventId = LogEvents.ImportRejected, EventName = nameof(LogEvents.ImportRejected), Level = LogLevel.Warning,
        Message = "Importação rejeitada (extensão {Extension}): {Reason}")]
    private static partial void LogImportRejected(ILogger logger, string extension, string reason);

    [LoggerMessage(EventId = LogEvents.ImportFailed, EventName = nameof(LogEvents.ImportFailed), Level = LogLevel.Error,
        Message = "Falha na importação")]
    private static partial void LogImportFailed(ILogger logger, Exception exception);
}
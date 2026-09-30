using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Invoices;

/// <param name="Total">Total da fatura: compras, tarifas e juros menos estornos (pagamentos não entram).</param>
/// <param name="Payments">Pagamentos lançados nesta fatura (informativo).</param>
public sealed record InvoiceDto(
    Guid Id,
    Guid CreditCardId,
    string CreditCardName,
    DateOnly ReferenceMonth,
    DateOnly StartDate,
    DateOnly ClosingDate,
    DateOnly DueDate,
    InvoiceStatus Status,
    decimal Total,
    decimal Payments,
    int TransactionCount);

public sealed class InvoiceService(
    IInvoiceRepository invoices,
    ICreditCardRepository creditCards,
    ISpendingQueries spending,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Faturas com lançamentos (ou abertas) de um cartão ou de todos, da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(Guid? creditCardId, CancellationToken cancellationToken)
    {
        var list = await invoices.ListAsync(creditCardId, cancellationToken);
        if (list.Count == 0)
        {
            return [];
        }

        var cards = (await creditCards.ListAsync(includeInactive: true, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
        var entries = await spending.GetEntriesAsync(list.Min(i => i.ReferenceMonth), list.Max(i => i.ReferenceMonth), creditCardId, cancellationToken);
        var byInvoice = entries.ToLookup(e => e.InvoiceId);
        var today = timeProvider.Today();

        return [.. list.Select(i =>
        {
            var items = byInvoice[i.Id].ToList();
            return new InvoiceDto(
                i.Id,
                i.CreditCardId,
                cards.GetValueOrDefault(i.CreditCardId, "?"),
                i.ReferenceMonth,
                i.StartDate,
                i.ClosingDate,
                i.DueDate,
                i.GetStatus(today),
                items.Sum(e => e.Spending),
                items.Where(e => e.Kind == TransactionKind.Payment).Sum(e => e.Amount),
                items.Count);
        })];
    }

    public Task MarkPaidAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, i => i.MarkPaid(timeProvider.UtcNow()), cancellationToken);

    public Task MarkUnpaidAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, i => i.MarkUnpaid(), cancellationToken);

    private async Task ChangeAsync(Guid id, Action<Invoice> change, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Fatura não encontrada.");
        change(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Invoices;

/// <param name="Total">Total da fatura: compras, tarifas e juros menos estornos (pagamentos não entram).</param>
/// <param name="Payments">Pagamentos lançados nesta fatura (informativo; normalmente quitam a fatura anterior).</param>
/// <param name="SettledByPayments">A fatura foi considerada paga pelos pagamentos lançados na fatura seguinte.</param>
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
    int TransactionCount,
    bool SettledByPayments);

public sealed class InvoiceService(
    IInvoiceRepository invoices,
    ICreditCardRepository creditCards,
    ISpendingQueries spending,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Faturas de um cartão ou de todos, da mais recente para a mais antiga.
    /// Uma fatura fechada é considerada paga quando os pagamentos lançados na fatura seguinte do mesmo cartão
    /// cobrem o seu total (o pagamento é feito depois do fechamento) ou quando o total é zero (ADR 0011).
    /// </summary>
    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(Guid? creditCardId, CancellationToken cancellationToken)
    {
        var list = await invoices.ListAsync(creditCardId, cancellationToken);
        if (list.Count == 0)
        {
            return [];
        }

        var cards = (await creditCards.ListAsync(includeInactive: true, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
        var entries = await spending.GetEntriesAsync(
            list.Min(i => i.ReferenceMonth), list.Max(i => i.ReferenceMonth).AddMonths(1), creditCardId, cancellationToken);
        var byInvoice = entries.ToLookup(e => e.InvoiceId);
        var paymentsByCardMonth = entries
            .Where(e => e.Kind == TransactionKind.Payment)
            .GroupBy(e => (e.CreditCardId, e.InvoiceMonth))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        var today = timeProvider.Today();

        return [.. list.Select(i =>
        {
            var items = byInvoice[i.Id].ToList();
            var total = items.Sum(e => e.Spending);
            var nextPayments = paymentsByCardMonth.GetValueOrDefault((i.CreditCardId, i.ReferenceMonth.AddMonths(1)));
            var settled = total <= 0 || nextPayments >= total;
            var status = i.GetStatus(today, settled);
            return new InvoiceDto(
                i.Id,
                i.CreditCardId,
                cards.GetValueOrDefault(i.CreditCardId, "?"),
                i.ReferenceMonth,
                i.StartDate,
                i.ClosingDate,
                i.DueDate,
                status,
                total,
                items.Where(e => e.Kind == TransactionKind.Payment).Sum(e => e.Amount),
                items.Count,
                status == InvoiceStatus.Paid && i.PaidAt is null);
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
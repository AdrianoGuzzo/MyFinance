using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Invoices;

/// <summary>Obtém as faturas de um cartão por mês de referência, criando as que ainda não existem (uma por cartão e mês).</summary>
internal static class InvoiceBook
{
    public static async Task<IReadOnlyDictionary<DateOnly, Invoice>> EnsureAsync(
        CreditCard card,
        IReadOnlyCollection<DateOnly> referenceMonths,
        IInvoiceRepository invoices,
        CancellationToken cancellationToken)
    {
        var months = referenceMonths.Distinct().ToList();
        var result = (await invoices.ListByMonthsAsync(card.Id, months, cancellationToken)).ToDictionary(i => i.ReferenceMonth);

        foreach (var month in months.Where(m => !result.ContainsKey(m)))
        {
            var invoice = Invoice.For(card, card.GetInvoicePeriodForMonth(month));
            invoices.Add(invoice);
            result[month] = invoice;
        }

        return result;
    }
}
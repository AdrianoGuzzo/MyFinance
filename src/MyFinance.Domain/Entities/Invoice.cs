using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Fatura de um cartão em um mês de referência (mês de vencimento, como os bancos exibem).
/// O total não é armazenado: é sempre a soma dos lançamentos da fatura, para nunca ficar desatualizado.
/// A situação é derivada das datas e do pagamento registrado (ver ADR 0011).
/// </summary>
public sealed class Invoice
{
    private Invoice() { } // EF Core

    public Guid Id { get; private set; }

    public Guid CreditCardId { get; private set; }

    /// <summary>Primeiro dia do mês de vencimento.</summary>
    public DateOnly ReferenceMonth { get; private set; }

    /// <summary>Lançamentos a partir desta data (inclusive)...</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>...até esta data (exclusive).</summary>
    public DateOnly ClosingDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    /// <summary>Quando o usuário marcou a fatura como paga (UTC).</summary>
    public DateTime? PaidAt { get; private set; }

    public InvoicePeriod Period => new(StartDate, ClosingDate, DueDate);

    public static Invoice For(CreditCard card, InvoicePeriod period)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(period);

        return new Invoice
        {
            Id = Guid.CreateVersion7(),
            CreditCardId = card.Id,
            ReferenceMonth = period.ReferenceMonth,
            StartDate = period.StartDate,
            ClosingDate = period.ClosingDate,
            DueDate = period.DueDate,
        };
    }

    /// <param name="settledByPayments">
    /// Os pagamentos importados (normalmente lançados na fatura seguinte) cobrem o total desta fatura, ou o total é zero.
    /// Uma fatura ainda aberta continua aberta.
    /// </param>
    public InvoiceStatus GetStatus(DateOnly today, bool settledByPayments = false) => this switch
    {
        _ when today < ClosingDate && PaidAt is null => InvoiceStatus.Open,
        { PaidAt: not null } => InvoiceStatus.Paid,
        _ when settledByPayments => InvoiceStatus.Paid,
        _ when today > DueDate => InvoiceStatus.Overdue,
        _ => InvoiceStatus.Closed,
    };

    public void MarkPaid(DateTime nowUtc) => PaidAt = Guard.Utc(nowUtc);

    public void MarkUnpaid() => PaidAt = null;

    public override string ToString() => $"{ReferenceMonth:MM/yyyy}";
}
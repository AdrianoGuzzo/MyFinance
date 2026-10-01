using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;

namespace MyFinance.Domain.Analysis;

/// <summary>
/// Projeção enxuta de um lançamento para as análises de gastos.
/// <paramref name="InvoiceMonth"/> é o mês de referência da fatura (competência usada em todas as análises).
/// </summary>
public sealed record SpendingEntry(
    Guid TransactionId,
    Guid CreditCardId,
    Guid InvoiceId,
    DateOnly InvoiceMonth,
    DateOnly Date,
    decimal Amount,
    TransactionKind Kind,
    Guid? CategoryId,
    string MerchantKey,
    string MerchantName,
    Guid? InstallmentPurchaseId,
    int? InstallmentNumber)
{
    /// <summary>Quanto o lançamento representa em gastos (ver <see cref="Services.Spending.AmountOf"/>).</summary>
    public decimal Spending => Services.Spending.AmountOf(Kind, Amount);
}

using MyFinance.Domain.Entities;

namespace MyFinance.Domain.Services;

/// <summary>
/// Encontra a compra parcelada à qual pertence uma parcela importada: mesmo estabelecimento, mesma quantidade de parcelas,
/// mesmo mês da 1ª parcela e valor igual (tolerância de <see cref="AmountTolerance"/> para o arredondamento da última parcela).
/// Uma compra que já tem aquela parcela vinculada é ignorada: trata-se de outra compra idêntica.
/// </summary>
public static class InstallmentMatcher
{
    public const decimal AmountTolerance = 1.00m;

    /// <param name="installmentAmount">Valor da parcela (positivo).</param>
    /// <param name="invoiceMonth">Mês da fatura em que a parcela foi lançada.</param>
    /// <param name="isTaken">Indica se a compra já tem a parcela de número informado.</param>
    public static InstallmentPurchase? FindMatch(
        IEnumerable<InstallmentPurchase> candidates,
        string merchantKey,
        InstallmentInfo installment,
        DateOnly invoiceMonth,
        decimal installmentAmount,
        Func<Guid, int, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(installment);
        ArgumentNullException.ThrowIfNull(isTaken);

        var firstMonth = FirstInvoiceMonth(installment, invoiceMonth);

        return candidates
            .Where(p => p.MerchantKey == merchantKey
                && p.InstallmentCount == installment.Count
                && p.FirstInvoiceMonth == firstMonth
                && Math.Abs(p.InstallmentAmount - installmentAmount) <= AmountTolerance
                && !isTaken(p.Id, installment.Number))
            .OrderBy(p => Math.Abs(p.InstallmentAmount - installmentAmount))
            .ThenBy(p => p.CreatedAt)
            .FirstOrDefault();
    }

    /// <summary>Mês da fatura da 1ª parcela, a partir da parcela lançada em <paramref name="invoiceMonth"/>.</summary>
    public static DateOnly FirstInvoiceMonth(InstallmentInfo installment, DateOnly invoiceMonth)
    {
        ArgumentNullException.ThrowIfNull(installment);
        return Months.Of(invoiceMonth).AddMonths(-(installment.Number - 1));
    }
}
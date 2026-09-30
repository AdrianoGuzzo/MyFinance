using MyFinance.Domain.Entities;

namespace MyFinance.Domain.Analysis;

/// <summary>Parcelas previstas para uma fatura futura.</summary>
/// <param name="Amount">Soma das parcelas ainda não lançadas previstas para o mês.</param>
/// <param name="Installments">Quantidade de parcelas previstas.</param>
public sealed record MonthCommitment(DateOnly Month, decimal Amount, int Installments);

/// <summary>
/// "Quanto das minhas próximas faturas já está comprometido?": projeta as parcelas das compras parceladas
/// nas faturas futuras. Parcelas já lançadas (importadas) não entram, pois já fazem parte do gasto real da fatura.
/// </summary>
public static class CommitmentProjector
{
    /// <param name="posted">Parcelas já lançadas: (compra, número da parcela).</param>
    public static IReadOnlyList<MonthCommitment> Project(
        IEnumerable<InstallmentPurchase> purchases,
        IReadOnlyList<DateOnly> months,
        IReadOnlySet<(Guid PurchaseId, int Number)> posted)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(months);
        ArgumentNullException.ThrowIfNull(posted);

        var list = purchases.ToList();
        return [.. months.Select(month =>
        {
            var due = list
                .Select(p => (Purchase: p, Number: p.NumberIn(month)))
                .Where(x => x.Number is { } n && !posted.Contains((x.Purchase.Id, n)))
                .ToList();

            return new MonthCommitment(Months.Of(month), due.Sum(x => x.Purchase.InstallmentAmount), due.Count);
        })];
    }

    /// <summary>
    /// Valor ainda a lançar das compras parceladas: parcelas de <paramref name="fromMonth"/> em diante que ainda não foram lançadas.
    /// Parcelas de meses anteriores nunca importadas (antes de o app ser usado) não contam.
    /// </summary>
    public static decimal Outstanding(
        IEnumerable<InstallmentPurchase> purchases, DateOnly fromMonth, IReadOnlySet<(Guid PurchaseId, int Number)> posted)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(posted);

        var from = Months.Of(fromMonth);
        return purchases.Sum(p => p.Schedule().Where(s => s.InvoiceMonth >= from && !posted.Contains((p.Id, s.Number))).Sum(s => s.Amount));
    }
}
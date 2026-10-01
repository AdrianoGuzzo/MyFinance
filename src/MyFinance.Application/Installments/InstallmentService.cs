using MyFinance.Application.Analysis;
using MyFinance.Domain;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Installments;

/// <param name="CurrentNumber">Parcela que cai na fatura do mês de referência (<c>null</c> se a compra ainda não começou ou já terminou).</param>
/// <param name="RemainingCount">Parcelas em faturas posteriores ao mês de referência.</param>
/// <param name="RemainingAmount">Valor dessas parcelas.</param>
public sealed record InstallmentPurchaseDto(
    Guid Id,
    Guid CreditCardId,
    string CreditCardName,
    string Description,
    decimal InstallmentAmount,
    int InstallmentCount,
    decimal TotalAmount,
    int? CurrentNumber,
    int RemainingCount,
    decimal RemainingAmount,
    DateOnly FirstInvoiceMonth,
    DateOnly LastInvoiceMonth);

/// <param name="Amount">Parcelas ainda não lançadas previstas para a fatura do mês.</param>
public sealed record CommitmentDto(DateOnly Month, decimal Amount, int Installments);

/// <summary>Compras parceladas e o comprometimento das próximas faturas.</summary>
public sealed class InstallmentService(
    IInstallmentPurchaseRepository installments,
    ITransactionRepository transactions,
    ICreditCardRepository creditCards,
    AnalysisLoader loader)
{
    public const int DefaultProjectionMonths = 6;

    /// <param name="includeFinished">Incluir compras cujas parcelas já terminaram.</param>
    public async Task<IReadOnlyList<InstallmentPurchaseDto>> ListAsync(bool includeFinished, CancellationToken cancellationToken)
    {
        var month = await loader.DefaultMonthAsync(cancellationToken);
        var cards = (await creditCards.ListAsync(includeInactive: true, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);

        return [.. (await installments.ListAsync(cancellationToken))
            .Where(p => includeFinished || p.LastInvoiceMonth >= month)
            .OrderByDescending(p => p.RemainingAmount(month))
            .ThenBy(p => p.Description, StringComparer.CurrentCulture)
            .Select(p => new InstallmentPurchaseDto(
                p.Id,
                p.CreditCardId,
                cards.GetValueOrDefault(p.CreditCardId, "?"),
                p.Description,
                p.InstallmentAmount,
                p.InstallmentCount,
                p.TotalAmount,
                p.NumberIn(month),
                p.RemainingCount(month),
                p.RemainingAmount(month),
                p.FirstInvoiceMonth,
                p.LastInvoiceMonth))];
    }

    /// <summary>Parcelas ainda não lançadas por fatura, a partir do mês de referência (a fatura atual).</summary>
    public async Task<IReadOnlyList<CommitmentDto>> GetCommitmentsAsync(int months, CancellationToken cancellationToken)
    {
        var month = await loader.DefaultMonthAsync(cancellationToken);
        var (purchases, posted) = await LoadAsync(cancellationToken);
        return [.. CommitmentProjector.Project(purchases, Months.Ending(month.AddMonths(months - 1), months), posted)
            .Select(c => new CommitmentDto(c.Month, c.Amount, c.Installments))];
    }

    /// <summary>Compras parceladas e as parcelas já lançadas (para projeções).</summary>
    internal async Task<(IReadOnlyList<InstallmentPurchase> Purchases, IReadOnlySet<(Guid PurchaseId, int Number)> Posted)> LoadAsync(
        CancellationToken cancellationToken)
    {
        var purchases = await installments.ListAsync(cancellationToken);
        var links = await transactions.GetInstallmentLinksAsync([.. purchases.Select(p => p.Id)], cancellationToken);
        return (purchases, links.Select(l => (l.PurchaseId, l.Number)).ToHashSet());
    }
}
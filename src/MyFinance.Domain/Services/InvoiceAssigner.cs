using MyFinance.Domain.Entities;

namespace MyFinance.Domain.Services;

/// <summary>Lançamento a atribuir a uma fatura: data informada pelo arquivo e parcela, se houver.</summary>
public sealed record InvoiceAssignmentItem(DateOnly Date, InstallmentInfo? Installment);

/// <summary>
/// Define a fatura (mês de referência) de cada lançamento de um arquivo.
/// <list type="bullet">
/// <item><description>Regra geral: a fatura em que a data cai, pelo fechamento do cartão.</description></item>
/// <item><description>Parcela k &gt; 1: alguns bancos informam a data <b>original</b> da compra. Se a fatura pela data
/// avançada em k − 1 meses coincide com a fatura predominante do arquivo (a das linhas sem parcela), é esse o caso e a
/// parcela vai para essa fatura; senão, a data já é a do lançamento e vale a regra geral.</description></item>
/// <item><description>O usuário pode escolher uma fatura para o arquivo inteiro (<c>overrideMonth</c>).</description></item>
/// </list>
/// </summary>
public static class InvoiceAssigner
{
    public static IReadOnlyList<DateOnly> Assign(CreditCard card, IReadOnlyList<InvoiceAssignmentItem> items, DateOnly? overrideMonth)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(items);

        if (overrideMonth is { } chosen)
        {
            return [.. items.Select(_ => Months.Of(chosen))];
        }

        var byDate = items.Select(i => card.GetInvoicePeriod(i.Date).ReferenceMonth).ToList();
        var dominant = byDate
            .Where((_, index) => items[index].Installment is null)
            .GroupBy(m => m)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .Select(g => (DateOnly?)g.Key)
            .FirstOrDefault();

        return [.. items.Select((item, index) =>
        {
            if (dominant is { } fileMonth && item.Installment is { Number: > 1 } installment
                && byDate[index].AddMonths(installment.Number - 1) == fileMonth)
            {
                return fileMonth;
            }

            return byDate[index];
        })];
    }
}
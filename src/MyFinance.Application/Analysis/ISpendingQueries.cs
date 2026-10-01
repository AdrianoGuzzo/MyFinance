using MyFinance.Domain.Analysis;

namespace MyFinance.Application.Analysis;

/// <summary>
/// Leitura dos lançamentos para análises (sem rastrear entidades), implementada na Infrastructure.
/// As regras de cálculo ficam no domínio (<c>MyFinance.Domain.Analysis</c>); aqui só se buscam os dados.
/// </summary>
public interface ISpendingQueries
{
    /// <summary>Lançamentos das faturas com mês de referência entre <paramref name="fromMonth"/> e <paramref name="toMonth"/> (inclusive).</summary>
    Task<IReadOnlyList<SpendingEntry>> GetEntriesAsync(
        DateOnly fromMonth, DateOnly toMonth, Guid? creditCardId, CancellationToken cancellationToken);

    /// <summary>Mês de referência da fatura mais antiga com lançamentos, ou <c>null</c> se não houver lançamentos.</summary>
    Task<DateOnly?> GetFirstMonthAsync(CancellationToken cancellationToken);
}

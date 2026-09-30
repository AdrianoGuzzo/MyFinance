using MyFinance.Application.Common;
using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Transactions;

public sealed record TransactionSearch
{
    public Guid? AccountId { get; init; }

    public Guid? CreditCardId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary>Filtra pela categoria e por suas subcategorias.</summary>
    public Guid? CategoryId { get; init; }

    public bool UncategorizedOnly { get; init; }

    /// <summary>Trecho da descrição.</summary>
    public string? Text { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record TransactionListItem(
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    TransactionType TransactionType,
    TransactionOwnerType OwnerType,
    Guid OwnerId,
    string OwnerName,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryColor);

/// <summary>Projeção enxuta de um lançamento, usada em cálculos (dashboard, fatura).</summary>
public sealed record TransactionSnapshot(DateOnly Date, decimal Amount, Guid? AccountId, Guid? CreditCardId, Guid? CategoryId);

/// <summary>Consultas de leitura (sem rastrear entidades), implementadas na Infrastructure.</summary>
public interface ITransactionQueries
{
    Task<PagedResult<TransactionListItem>> SearchAsync(TransactionSearch search, CancellationToken cancellationToken);

    /// <summary>Lançamentos com data entre <paramref name="fromDate"/> e <paramref name="toDate"/> (inclusive).</summary>
    Task<IReadOnlyList<TransactionSnapshot>> GetSnapshotsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);

    /// <summary>Soma dos lançamentos de cada conta bancária com data até <paramref name="untilDate"/> (inclusive; <c>null</c> = todos).</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetAccountTotalsAsync(DateOnly? untilDate, CancellationToken cancellationToken);

    /// <summary>Soma das saídas (valores negativos) da conta/cartão no período (inclusive). Resultado ≤ 0.</summary>
    Task<decimal> SumOutflowsAsync(TransactionOwner owner, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}
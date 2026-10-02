using MyFinance.Application.Common;
using MyFinance.Domain.Enums;

namespace MyFinance.Application.Transactions;

public sealed record TransactionSearch
{
    public Guid? CreditCardId { get; init; }

    public Guid? InvoiceId { get; init; }

    /// <summary>Mês de referência da fatura (competência), inclusive.</summary>
    public DateOnly? FromMonth { get; init; }

    /// <summary>Mês de referência da fatura (competência), inclusive.</summary>
    public DateOnly? ToMonth { get; init; }

    /// <summary>Filtra pela categoria e por suas subcategorias.</summary>
    public Guid? CategoryId { get; init; }

    public bool UncategorizedOnly { get; init; }

    /// <summary>Ignora pagamentos de fatura (não são gastos e não precisam de categoria).</summary>
    public bool ExcludePayments { get; init; }

    public TransactionKind? Kind { get; init; }

    /// <summary>Trecho da descrição.</summary>
    public string? Text { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

/// <param name="SpendingAmount">Quanto conta como gasto: positivo para compras, negativo para estornos, zero para pagamentos.</param>
public sealed record TransactionListItem(
    Guid Id,
    DateOnly Date,
    DateOnly InvoiceMonth,
    string Description,
    string MerchantName,
    decimal Amount,
    decimal SpendingAmount,
    TransactionKind Kind,
    Guid CreditCardId,
    string CreditCardName,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    int? InstallmentNumber,
    int? InstallmentCount);

/// <summary>Consultas de tela (sem rastrear entidades), implementadas na Infrastructure.</summary>
public interface ITransactionQueries
{
    Task<PagedResult<TransactionListItem>> SearchAsync(TransactionSearch search, CancellationToken cancellationToken);
}
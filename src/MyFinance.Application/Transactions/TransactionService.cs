using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Transactions;

public sealed class TransactionService(
    ITransactionRepository transactions,
    ICategoryRepository categories,
    ITransactionQueries queries,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<PagedResult<TransactionListItem>> SearchAsync(TransactionSearch search, CancellationToken cancellationToken) =>
        queries.SearchAsync(search, cancellationToken);

    /// <summary>Atribui a categoria ao lançamento; <paramref name="categoryId"/> nulo remove a categoria.</summary>
    public async Task CategorizeAsync(Guid transactionId, Guid? categoryId, CancellationToken cancellationToken)
    {
        var transaction = await transactions.GetByIdAsync(transactionId, cancellationToken)
            ?? throw new ValidationException("Lançamento não encontrado.");
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (categoryId is { } id)
        {
            var category = await categories.GetByIdAsync(id, cancellationToken)
                ?? throw new ValidationException("Categoria não encontrada.");
            transaction.Categorize(category, now);
        }
        else
        {
            transaction.RemoveCategory(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Interfaces;

public sealed record CategorizationInput(TransactionOwner Owner, DateOnly Date, decimal Amount, string Description);

/// <summary>
/// Sugere a categoria de um lançamento. No MVP não há implementação automática;
/// futuramente: regras ("descrição contém IFOOD" → Alimentação &gt; Delivery) e, depois, IA local.
/// </summary>
public interface ICategorizationService
{
    /// <returns>Id da categoria sugerida, ou <c>null</c> quando não houver sugestão.</returns>
    Task<Guid?> SuggestCategoryAsync(CategorizationInput input, CancellationToken cancellationToken);
}
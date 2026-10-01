namespace MyFinance.Domain.Interfaces;

public sealed record CategorizationInput(Guid CreditCardId, DateOnly Date, decimal Amount, string Description);

/// <summary>
/// Sugere a categoria de um lançamento. Implementação padrão: regras configuráveis
/// ("descrição contém IFOOD" → Alimentação &gt; Delivery); futuramente, IA local.
/// </summary>
public interface ICategorizationService
{
    /// <returns>Id da categoria sugerida (ativa), ou <c>null</c> quando não houver sugestão.</returns>
    Task<Guid?> SuggestCategoryAsync(CategorizationInput input, CancellationToken cancellationToken);
}
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Categories;

/// <summary>
/// Implementação do MVP: não sugere categorias. Na fase 2 será substituída por regras
/// ("descrição contém IFOOD" → Alimentação &gt; Delivery) sem alterar os casos de uso.
/// </summary>
public sealed class NoCategorizationService : ICategorizationService
{
    public Task<Guid?> SuggestCategoryAsync(CategorizationInput input, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(null);
}
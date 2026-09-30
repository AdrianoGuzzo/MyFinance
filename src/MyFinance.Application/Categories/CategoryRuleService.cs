using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;

namespace MyFinance.Application.Categories;

/// <param name="Pattern">Texto procurado na descrição (sem diferenciar maiúsculas e acentos).</param>
/// <param name="Priority">0 a 1000; a maior vence quando mais de uma regra casa.</param>
public sealed record SaveCategoryRuleCommand(string Pattern, Guid CategoryId, int Priority);

public sealed record CategoryRuleDto(Guid Id, string Pattern, Guid CategoryId, string CategoryName, int Priority, bool IsActive);

/// <summary>Regras de categorização automática: "IFOOD" → Alimentação &gt; Delivery.</summary>
public sealed class CategoryRuleService(
    ICategoryRuleRepository rules,
    ICategoryRepository categories,
    ITransactionRepository transactions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CategoryRuleDto>> ListAsync(CancellationToken cancellationToken)
    {
        var all = await categories.ListAsync(includeInactive: true, cancellationToken);
        var names = CategoryService.ToTree(all).ToDictionary(c => c.Id, c => c.FullName);

        return [.. (await rules.ListAsync(includeInactive: true, cancellationToken))
            .Select(r => new CategoryRuleDto(r.Id, r.Pattern, r.CategoryId, names.GetValueOrDefault(r.CategoryId, "?"), r.Priority, r.IsActive))];
    }

    public async Task<Guid> CreateAsync(SaveCategoryRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rule = CategoryRule.Create(command.Pattern, await GetCategoryAsync(command.CategoryId, cancellationToken), command.Priority, timeProvider.UtcNow());
        rules.Add(rule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }

    public async Task UpdateAsync(Guid id, SaveCategoryRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rule = await GetAsync(id, cancellationToken);
        rule.Update(command.Pattern, await GetCategoryAsync(command.CategoryId, cancellationToken), command.Priority);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task ActivateAsync(Guid id, CancellationToken cancellationToken) => ChangeAsync(id, r => r.Activate(), cancellationToken);

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken) => ChangeAsync(id, r => r.Deactivate(), cancellationToken);

    /// <summary>Regras são configuração: excluir não afeta lançamentos já categorizados.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        rules.Remove(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Aplica as regras ativas aos lançamentos ainda sem categoria. Nunca altera uma categoria já atribuída.
    /// </summary>
    /// <returns>Quantidade de lançamentos categorizados.</returns>
    public async Task<int> ApplyToUncategorizedAsync(CancellationToken cancellationToken)
    {
        var active = await rules.ListAsync(includeInactive: false, cancellationToken);
        var activeCategories = (await categories.ListAsync(includeInactive: false, cancellationToken)).ToDictionary(c => c.Id);
        var usable = active.Where(r => activeCategories.ContainsKey(r.CategoryId)).ToList();
        var now = timeProvider.UtcNow();
        var count = 0;

        foreach (var transaction in await transactions.ListUncategorizedAsync(cancellationToken))
        {
            if (CategoryRuleMatcher.Match(usable, transaction.Description) is { } rule)
            {
                transaction.Categorize(activeCategories[rule.CategoryId], now);
                count++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task ChangeAsync(Guid id, Action<CategoryRule> change, CancellationToken cancellationToken)
    {
        change(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<CategoryRule> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await rules.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Regra não encontrada.");

    private async Task<Category> GetCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        await categories.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Categoria não encontrada.");
}
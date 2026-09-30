using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;

namespace MyFinance.Application.Categories;

/// <summary>
/// Categorização automática por regras configuráveis (<see cref="CategoryRule"/>).
/// Regras e categorias são carregadas uma vez por escopo (uma importação), evitando uma consulta por lançamento.
/// Regras de categorias desativadas são ignoradas.
/// </summary>
public sealed class RuleBasedCategorizationService(ICategoryRuleRepository rules, ICategoryRepository categories) : ICategorizationService
{
    private IReadOnlyList<CategoryRule>? _usableRules;

    public async Task<Guid?> SuggestCategoryAsync(CategorizationInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_usableRules is null)
        {
            var active = (await categories.ListAsync(includeInactive: false, cancellationToken)).Select(c => c.Id).ToHashSet();
            _usableRules = [.. (await rules.ListAsync(includeInactive: false, cancellationToken)).Where(r => active.Contains(r.CategoryId))];
        }

        return CategoryRuleMatcher.Match(_usableRules, input.Description)?.CategoryId;
    }
}
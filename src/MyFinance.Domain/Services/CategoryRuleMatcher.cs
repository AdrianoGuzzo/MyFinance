using MyFinance.Domain.Entities;

namespace MyFinance.Domain.Services;

/// <summary>
/// Escolhe a regra que categoriza uma descrição: entre as regras ativas que casam, vence a de maior prioridade;
/// empate → padrão mais longo (mais específico); empate → regra mais antiga.
/// </summary>
public static class CategoryRuleMatcher
{
    public static CategoryRule? Match(IEnumerable<CategoryRule> rules, string? description)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var normalized = TransactionFingerprint.NormalizeDescription(description);
        if (normalized.Length == 0)
        {
            return null;
        }

        return rules
            .Where(r => r.IsActive && r.Matches(normalized))
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.Pattern.Length)
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .FirstOrDefault();
    }
}
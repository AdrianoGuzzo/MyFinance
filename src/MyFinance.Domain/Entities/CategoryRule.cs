using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Regra de categorização automática: "descrição contém IFOOD" → Alimentação &gt; Delivery.
/// O padrão é comparado com a descrição normalizada (maiúsculas, sem acentos) e precisa começar
/// no início de uma palavra: "IOF" casa com "IOF COMPRA EXTERIOR", mas não com "RIOFERTIL".
/// </summary>
public sealed class CategoryRule
{
    public const int PatternMaxLength = 100;
    public const int MaxPriority = 1000;

    private CategoryRule() { } // EF Core

    public Guid Id { get; private set; }

    /// <summary>Texto normalizado (maiúsculas, sem acentos).</summary>
    public string Pattern { get; private set; } = string.Empty;

    public Guid CategoryId { get; private set; }

    /// <summary>Maior prioridade vence quando mais de uma regra casa.</summary>
    public int Priority { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static CategoryRule Create(string pattern, Category category, int priority, DateTime createdAtUtc)
    {
        var rule = new CategoryRule
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            CreatedAt = Guard.Utc(createdAtUtc),
        };
        rule.Update(pattern, category, priority);
        return rule;
    }

    public void Update(string pattern, Category category, int priority)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (!category.IsActive)
        {
            throw new DomainException($"A categoria \"{category.Name}\" está desativada.");
        }

        if (priority is < 0 or > MaxPriority)
        {
            throw new DomainException($"A prioridade deve estar entre 0 e {MaxPriority}.");
        }

        var normalized = TransactionFingerprint.NormalizeDescription(pattern);
        if (normalized.Length < 2)
        {
            throw new DomainException("O padrão da regra deve ter ao menos 2 caracteres.");
        }

        Pattern = Guard.Required(normalized, PatternMaxLength, "O padrão da regra");
        CategoryId = category.Id;
        Priority = priority;
    }

    /// <param name="normalizedDescription">Descrição já normalizada por <see cref="TransactionFingerprint.NormalizeDescription"/>.</param>
    public bool Matches(string normalizedDescription)
    {
        ArgumentNullException.ThrowIfNull(normalizedDescription);

        var index = normalizedDescription.IndexOf(Pattern, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(normalizedDescription[index - 1]))
            {
                return true;
            }

            index = normalizedDescription.IndexOf(Pattern, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Gasto recorrente detectado (assinaturas, academia...). A detecção é recalculada a partir do histórico;
/// a entidade guarda o que o usuário decidiu (classificação ou descarte).
/// </summary>
public sealed class RecurringExpense
{
    public const int NameMaxLength = 200;

    private RecurringExpense() { } // EF Core

    public Guid Id { get; private set; }

    public string MerchantKey { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public decimal EstimatedMonthlyAmount { get; private set; }

    public Guid? CategoryId { get; private set; }

    public RecurringClassification Classification { get; private set; }

    /// <summary>O usuário indicou que não é um gasto recorrente.</summary>
    public bool IsDismissed { get; private set; }

    public DateOnly LastSeenMonth { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public decimal EstimatedAnnualAmount => EstimatedMonthlyAmount * 12;

    public static RecurringExpense Create(
        string merchantKey, string displayName, decimal estimatedMonthlyAmount, Guid? categoryId, DateOnly lastSeenMonth, DateTime nowUtc)
    {
        var expense = new RecurringExpense
        {
            Id = Guid.CreateVersion7(),
            MerchantKey = Guard.Required(merchantKey, NameMaxLength, "O estabelecimento"),
            Classification = RecurringClassification.Unclassified,
            CreatedAt = Guard.Utc(nowUtc),
        };
        expense.Refresh(displayName, estimatedMonthlyAmount, categoryId, lastSeenMonth, nowUtc);
        return expense;
    }

    /// <summary>Atualiza os dados detectados, preservando a classificação e o descarte do usuário.</summary>
    public void Refresh(string displayName, decimal estimatedMonthlyAmount, Guid? categoryId, DateOnly lastSeenMonth, DateTime nowUtc)
    {
        if (estimatedMonthlyAmount <= 0)
        {
            throw new DomainException("O valor mensal estimado deve ser positivo.");
        }

        DisplayName = Guard.Required(displayName, NameMaxLength, "O nome");
        EstimatedMonthlyAmount = decimal.Round(estimatedMonthlyAmount, 2);
        CategoryId = categoryId;
        LastSeenMonth = Months.Of(lastSeenMonth);
        UpdatedAt = Guard.Utc(nowUtc);
    }

    public void Classify(RecurringClassification classification, DateTime nowUtc)
    {
        Classification = Guard.Defined(classification, "Classificação");
        UpdatedAt = Guard.Utc(nowUtc);
    }

    public void Dismiss(DateTime nowUtc)
    {
        IsDismissed = true;
        UpdatedAt = Guard.Utc(nowUtc);
    }

    public void Restore(DateTime nowUtc)
    {
        IsDismissed = false;
        UpdatedAt = Guard.Utc(nowUtc);
    }
}
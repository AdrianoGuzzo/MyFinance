using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.Entities;

/// <summary>Meta de economia mensal (ex.: economizar R$ 1.000/mês).</summary>
public sealed class FinancialGoal
{
    public const int NameMaxLength = 100;

    private FinancialGoal() { } // EF Core

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal MonthlyTarget { get; private set; }

    public DateOnly StartMonth { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public decimal AnnualTarget => MonthlyTarget * 12;

    public static FinancialGoal Create(string name, decimal monthlyTarget, DateOnly startMonth, DateTime createdAtUtc)
    {
        var goal = new FinancialGoal
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            CreatedAt = Guard.Utc(createdAtUtc),
        };
        goal.Update(name, monthlyTarget, startMonth);
        return goal;
    }

    public void Update(string name, decimal monthlyTarget, DateOnly startMonth)
    {
        if (monthlyTarget <= 0)
        {
            throw new DomainException("O objetivo mensal deve ser maior que zero.");
        }

        Name = Guard.Required(name, NameMaxLength, "O nome da meta");
        MonthlyTarget = Guard.Money(monthlyTarget, "O objetivo mensal");
        StartMonth = Months.Of(startMonth);
    }

    public void Deactivate() => IsActive = false;
}
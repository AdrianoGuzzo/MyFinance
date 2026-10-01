using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.Entities;

/// <summary>Consumo de um limite no mês.</summary>
/// <param name="Percent">Usado / limite (1 = 100%).</param>
public sealed record LimitEvaluation(decimal Limit, decimal Used, decimal Available, decimal Excess, decimal Percent, LimitStatus Status);

/// <summary>Limite mensal de gastos de uma categoria (inclui as subcategorias quando é categoria principal).</summary>
public sealed class SpendingLimit
{
    /// <summary>A partir deste consumo o limite está "próximo".</summary>
    public const decimal NearThreshold = 0.8m;

    private SpendingLimit() { } // EF Core

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public decimal MonthlyAmount { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static SpendingLimit Create(Category category, decimal monthlyAmount, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(category);

        var limit = new SpendingLimit
        {
            Id = Guid.CreateVersion7(),
            CategoryId = category.Id,
            IsActive = true,
            CreatedAt = Guard.Utc(createdAtUtc),
        };
        limit.ChangeAmount(monthlyAmount);
        return limit;
    }

    public void ChangeAmount(decimal monthlyAmount)
    {
        if (monthlyAmount <= 0)
        {
            throw new DomainException("O limite deve ser maior que zero.");
        }

        MonthlyAmount = Guard.Money(monthlyAmount, "O limite");
    }

    /// <param name="used">Gasto da categoria no mês.</param>
    public LimitEvaluation Evaluate(decimal used) => Evaluate(MonthlyAmount, used);

    public static LimitEvaluation Evaluate(decimal limit, decimal used)
    {
        var percent = limit == 0 ? 0 : used / limit;
        var status = used > limit ? LimitStatus.Exceeded
            : percent >= NearThreshold ? LimitStatus.Near
            : LimitStatus.Within;

        return new LimitEvaluation(
            limit,
            used,
            Available: Math.Max(0, limit - used),
            Excess: Math.Max(0, used - limit),
            percent,
            status);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
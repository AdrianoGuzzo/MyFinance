using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.ValueObjects;

/// <summary>
/// Dia do mês (1–31) usado em fechamento/vencimento de fatura.
/// Em meses mais curtos, o dia é ajustado para o último dia do mês.
/// </summary>
public readonly record struct DayOfMonth
{
    private DayOfMonth(int value) => Value = value;

    public int Value { get; }

    public static DayOfMonth Create(int value)
    {
        if (value is < 1 or > 31)
        {
            throw new DomainException("O dia deve estar entre 1 e 31.");
        }

        return new DayOfMonth(value);
    }

    public DateOnly In(int year, int month) => new(year, month, Math.Min(Value, DateTime.DaysInMonth(year, month)));

    public DateOnly In(DateOnly monthReference) => In(monthReference.Year, monthReference.Month);
}
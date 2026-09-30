namespace MyFinance.Domain;

/// <summary>Meses são representados pelo primeiro dia (ex.: outubro/2026 = 2026-10-01).</summary>
public static class Months
{
    public static DateOnly Of(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>Quantidade de meses de <paramref name="from"/> até <paramref name="to"/> (negativa se anterior).</summary>
    public static int Between(DateOnly from, DateOnly to) => ((to.Year - from.Year) * 12) + to.Month - from.Month;

    /// <summary><paramref name="count"/> meses consecutivos terminando em <paramref name="last"/> (inclusive), em ordem crescente.</summary>
    public static IReadOnlyList<DateOnly> Ending(DateOnly last, int count)
    {
        var end = Of(last);
        return [.. Enumerable.Range(0, Math.Max(0, count)).Select(i => end.AddMonths(i - count + 1))];
    }
}
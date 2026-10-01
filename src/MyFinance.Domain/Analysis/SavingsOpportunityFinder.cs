using System.Globalization;

namespace MyFinance.Domain.Analysis;

public enum OpportunitySource
{
    /// <summary>Categoria com gasto acima da própria média.</summary>
    AboveAverage = 1,

    /// <summary>Limite de gastos excedido.</summary>
    LimitExceeded = 2,

    /// <summary>Gastos recorrentes marcados como Opcional ou Avaliar.</summary>
    Recurring = 3,
}

/// <summary>
/// Possível oportunidade de economia. É uma <b>informação</b> calculada dos dados — não uma recomendação obrigatória.
/// </summary>
public sealed record SavingsOpportunity(OpportunitySource Source, Guid? CategoryId, string Title, string Detail, decimal MonthlyPotential);

/// <summary>Limite excedido no mês: categoria e excedente.</summary>
public sealed record ExceededLimit(CategoryKey Category, decimal Limit, decimal Used);

/// <summary>Gastos recorrentes considerados dispensáveis ou a avaliar.</summary>
public sealed record RecurringSummary(int OptionalCount, decimal OptionalMonthly, int TotalCount, decimal TotalMonthly);

/// <param name="HistoryMonths">Meses usados na média das comparações.</param>
/// <param name="VersusAverage">Categorias (nível mais detalhado) comparadas com a média.</param>
public sealed record OpportunityInput(
    int HistoryMonths,
    IReadOnlyList<CategoryComparison> VersusAverage,
    IReadOnlyList<ExceededLimit> ExceededLimits,
    RecurringSummary Recurring);

/// <summary>
/// Procura oportunidades de economia no histórico:
/// <list type="number">
/// <item><description>categoria com gasto atual acima de <see cref="AboveAverageFactor"/> × a média e diferença ≥
/// <see cref="MinimumPotential"/> (exige <see cref="InsightGenerator.MinimumHistoryForAverage"/> meses de histórico) —
/// potencial = gasto atual − média;</description></item>
/// <item><description>limite excedido — potencial = excedente (se a categoria já aparece pelo item 1, fica o maior);</description></item>
/// <item><description>gastos recorrentes marcados como Opcional/Avaliar — potencial = soma mensal.</description></item>
/// </list>
/// </summary>
public static class SavingsOpportunityFinder
{
    public const decimal AboveAverageFactor = 1.15m;
    public const decimal MinimumPotential = 50m;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<SavingsOpportunity> Find(OpportunityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var byCategory = new Dictionary<Guid, SavingsOpportunity>();

        if (input.HistoryMonths >= InsightGenerator.MinimumHistoryForAverage)
        {
            foreach (var c in input.VersusAverage.Where(c => c.Category.Id is not null
                && c.Baseline > 0 && c.Current > c.Baseline * AboveAverageFactor && c.Difference >= MinimumPotential))
            {
                byCategory[c.Category.Id!.Value] = new SavingsOpportunity(
                    OpportunitySource.AboveAverage,
                    c.Category.Id,
                    c.Category.Name,
                    $"Média dos últimos {input.HistoryMonths} meses: {Money(c.Baseline)}. Gasto atual: {Money(c.Current)} ({Percent(c.Percent)}).",
                    c.Difference);
            }
        }

        foreach (var limit in input.ExceededLimits.Where(l => l.Category.Id is not null && l.Used > l.Limit))
        {
            var excess = limit.Used - limit.Limit;
            var id = limit.Category.Id!.Value;
            if (!byCategory.TryGetValue(id, out var existing) || existing.MonthlyPotential < excess)
            {
                byCategory[id] = new SavingsOpportunity(
                    OpportunitySource.LimitExceeded,
                    id,
                    limit.Category.Name,
                    $"Limite de {Money(limit.Limit)} excedido em {Money(excess)} (gasto: {Money(limit.Used)}).",
                    excess);
            }
        }

        var result = byCategory.Values.ToList();
        if (input.Recurring.OptionalCount > 0 && input.Recurring.OptionalMonthly > 0)
        {
            result.Add(new SavingsOpportunity(
                OpportunitySource.Recurring,
                null,
                "Assinaturas e recorrentes",
                $"{input.Recurring.OptionalCount} gasto(s) recorrente(s) marcado(s) como Opcional ou Avaliar: {Money(input.Recurring.OptionalMonthly)}/mês "
                + $"({Money(input.Recurring.OptionalMonthly * 12)}/ano).",
                input.Recurring.OptionalMonthly));
        }

        return [.. result.OrderByDescending(o => o.MonthlyPotential).ThenBy(o => o.Title, StringComparer.Ordinal)];
    }

    private static string Money(decimal value) => value.ToString("C", PtBr);

    private static string Percent(decimal? ratio) => ratio is { } r ? (r > 0 ? "+" : string.Empty) + (r * 100).ToString("0", PtBr) + "%" : "novo";
}
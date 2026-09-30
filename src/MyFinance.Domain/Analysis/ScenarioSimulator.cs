using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.Analysis;

/// <summary>Gasto mensal típico de uma categoria (base do simulador).</summary>
public sealed record CategoryBaseline(CategoryKey Category, decimal MonthlyAmount);

/// <param name="Reduction">Redução simulada (0,15 = 15%).</param>
public sealed record ScenarioLine(CategoryKey Category, decimal MonthlyAmount, decimal Reduction, decimal MonthlySavings);

public sealed record ScenarioResult(
    decimal CurrentMonthly, decimal MonthlySavings, decimal ProjectedMonthly, IReadOnlyList<ScenarioLine> Lines)
{
    public decimal AnnualSavings => MonthlySavings * 12;
}

/// <summary>
/// "E se eu reduzir alimentação em 15%?": aplica reduções percentuais sobre o gasto mensal típico de cada categoria,
/// calculado do histórico real (média dos meses anteriores).
/// </summary>
public static class ScenarioSimulator
{
    /// <param name="reductions">Redução por categoria (id), entre 0 e 1. Categorias ausentes não são reduzidas.</param>
    public static ScenarioResult Simulate(IEnumerable<CategoryBaseline> baselines, IReadOnlyDictionary<Guid, decimal> reductions)
    {
        ArgumentNullException.ThrowIfNull(baselines);
        ArgumentNullException.ThrowIfNull(reductions);

        if (reductions.Values.Any(r => r is < 0 or > 1))
        {
            throw new DomainException("A redução deve estar entre 0% e 100%.");
        }

        var lines = baselines
            .Select(b =>
            {
                var reduction = b.Category.Id is { } id ? reductions.GetValueOrDefault(id) : 0m;
                return new ScenarioLine(b.Category, b.MonthlyAmount, reduction, decimal.Round(b.MonthlyAmount * reduction, 2));
            })
            .ToList();

        var current = lines.Sum(l => l.MonthlyAmount);
        var savings = lines.Sum(l => l.MonthlySavings);
        return new ScenarioResult(current, savings, current - savings, lines);
    }
}

/// <summary>Oportunidade na estratégia, com o potencial acumulado até ela.</summary>
public sealed record GoalStep(SavingsOpportunity Opportunity, decimal Cumulative, bool ReachesGoal);

/// <param name="Gap">Quanto falta para a meta com as oportunidades identificadas (0 quando cobre).</param>
public sealed record GoalPlan(decimal MonthlyTarget, decimal IdentifiedPotential, decimal Gap, IReadOnlyList<GoalStep> Steps)
{
    public bool IsCovered => Gap == 0;
}

/// <summary>Compara a meta de economia com as oportunidades identificadas (da maior para a menor).</summary>
public static class GoalPlanner
{
    public static GoalPlan Plan(decimal monthlyTarget, IEnumerable<SavingsOpportunity> opportunities)
    {
        ArgumentNullException.ThrowIfNull(opportunities);

        var cumulative = 0m;
        var reached = false;
        var steps = new List<GoalStep>();
        foreach (var opportunity in opportunities.OrderByDescending(o => o.MonthlyPotential))
        {
            cumulative += opportunity.MonthlyPotential;
            var reachesNow = !reached && cumulative >= monthlyTarget;
            reached |= reachesNow;
            steps.Add(new GoalStep(opportunity, cumulative, reachesNow));
        }

        return new GoalPlan(monthlyTarget, cumulative, Math.Max(0, monthlyTarget - cumulative), steps);
    }
}
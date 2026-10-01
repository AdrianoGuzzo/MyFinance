using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Strategy;

public sealed record GoalDto(Guid Id, string Name, decimal MonthlyTarget, decimal AnnualTarget);

public sealed record OpportunityStepDto(
    OpportunitySource Source, Guid? CategoryId, string Title, string Detail, decimal MonthlyPotential, decimal Cumulative, bool ReachesGoal);

/// <summary>Gasto mensal típico de uma categoria principal (média dos meses anteriores), base do simulador.</summary>
public sealed record ScenarioBaselineDto(Guid? CategoryId, string Name, string Color, decimal MonthlyAmount);

public sealed record ScenarioLineDto(Guid? CategoryId, string Name, decimal MonthlyAmount, decimal Reduction, decimal MonthlySavings);

public sealed record ScenarioResultDto(
    decimal CurrentMonthly, decimal MonthlySavings, decimal AnnualSavings, decimal ProjectedMonthly, IReadOnlyList<ScenarioLineDto> Lines);

/// <param name="Gap">Quanto falta para a meta com o potencial identificado; <c>null</c> sem meta.</param>
/// <param name="HistoryMonths">Meses de histórico usados nas médias (0 = base é o próprio mês).</param>
public sealed record StrategyDto(
    DateOnly ReferenceMonth,
    GoalDto? Goal,
    IReadOnlyList<OpportunityStepDto> Opportunities,
    decimal IdentifiedPotential,
    decimal? Gap,
    int HistoryMonths,
    IReadOnlyList<ScenarioBaselineDto> Baselines);

/// <summary>
/// Estratégia de economia: meta mensal, possíveis oportunidades (informação, nunca obrigação) e simulação de cenários
/// com base no histórico real.
/// </summary>
public sealed class StrategyService(
    IFinancialGoalRepository goals,
    AnalysisLoader loader,
    SavingsAnalysis savings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<StrategyDto> GetAsync(CancellationToken cancellationToken)
    {
        var context = await LoadAsync(cancellationToken);
        var goal = await goals.GetActiveAsync(cancellationToken);
        var opportunities = await savings.OpportunitiesAsync(context, cancellationToken);
        var plan = GoalPlanner.Plan(goal?.MonthlyTarget ?? 0m, opportunities);

        return new StrategyDto(
            context.ReferenceMonth,
            goal is null ? null : new GoalDto(goal.Id, goal.Name, goal.MonthlyTarget, goal.AnnualTarget),
            [.. plan.Steps.Select(s => new OpportunityStepDto(
                s.Opportunity.Source, s.Opportunity.CategoryId, s.Opportunity.Title, s.Opportunity.Detail, s.Opportunity.MonthlyPotential,
                s.Cumulative, goal is not null && s.ReachesGoal))],
            plan.IdentifiedPotential,
            goal is null ? null : plan.Gap,
            context.BaselineMonths.Count,
            [.. Baselines(context).Select(b => new ScenarioBaselineDto(b.Category.Id, b.Category.Name, b.Category.Color, b.MonthlyAmount))]);
    }

    /// <summary>Define a meta de economia (substitui a meta ativa).</summary>
    public async Task SetGoalAsync(string name, decimal monthlyTarget, CancellationToken cancellationToken)
    {
        var month = await loader.DefaultMonthAsync(cancellationToken);
        if (await goals.GetActiveAsync(cancellationToken) is { } current)
        {
            current.Update(name, monthlyTarget, month);
        }
        else
        {
            goals.Add(FinancialGoal.Create(name, monthlyTarget, month, timeProvider.UtcNow()));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearGoalAsync(CancellationToken cancellationToken)
    {
        if (await goals.GetActiveAsync(cancellationToken) is { } current)
        {
            current.Deactivate();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <param name="reductions">Redução por categoria principal, entre 0 e 1 (0,15 = 15%).</param>
    public async Task<ScenarioResultDto> SimulateAsync(IReadOnlyDictionary<Guid, decimal> reductions, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(cancellationToken);
        var result = ScenarioSimulator.Simulate(Baselines(context), reductions);

        return new ScenarioResultDto(
            result.CurrentMonthly,
            result.MonthlySavings,
            result.AnnualSavings,
            result.ProjectedMonthly,
            [.. result.Lines.Select(l => new ScenarioLineDto(l.Category.Id, l.Category.Name, l.MonthlyAmount, l.Reduction, l.MonthlySavings))]);
    }

    private async Task<AnalysisContext> LoadAsync(CancellationToken cancellationToken) =>
        await loader.LoadAsync(await loader.DefaultMonthAsync(cancellationToken), SpendingCalculator.AverageWindow, 0, cancellationToken);

    /// <summary>Média dos meses anteriores por categoria principal; sem histórico, o próprio mês.</summary>
    private static IReadOnlyList<CategoryBaseline> Baselines(AnalysisContext context)
    {
        IReadOnlyList<DateOnly> months = context.BaselineMonths.Count > 0 ? context.BaselineMonths : [context.ReferenceMonth];
        var set = months.ToHashSet();

        return [.. context.Entries
            .Where(e => set.Contains(e.InvoiceMonth))
            .GroupBy(e => context.Categories.Resolve(e.CategoryId, CategoryLevel.Root))
            .Select(g => new CategoryBaseline(g.Key, decimal.Round(g.Sum(e => e.Spending) / months.Count, 2)))
            .Where(b => b.MonthlyAmount > 0)
            .OrderByDescending(b => b.MonthlyAmount)];
    }
}
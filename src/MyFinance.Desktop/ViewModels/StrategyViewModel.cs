using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Strategy;
using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.ViewModels;

public sealed record OpportunityStepItem(string Title, string Detail, decimal MonthlyPotential, decimal Cumulative, bool ReachesGoal);

/// <summary>Categoria do simulador: gasto mensal típico e a redução a simular (%).</summary>
public sealed partial class ScenarioRowViewModel(ScenarioBaselineDto baseline) : ViewModelBase
{
    [ObservableProperty]
    private decimal? _reductionPercent = 0;

    [ObservableProperty]
    private decimal _savings;

    public ScenarioBaselineDto Baseline { get; } = baseline;
}

/// <summary>
/// Estratégia de economia: meta mensal, possíveis oportunidades e simulador de cenários.
/// Tudo é informação para o usuário decidir — nenhuma redução é apresentada como obrigatória.
/// </summary>
public sealed partial class StrategyViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    private string _goalName = "Economizar";

    [ObservableProperty]
    private decimal? _goalTarget = 1000m;

    [ObservableProperty]
    private bool _hasGoal;

    [ObservableProperty]
    private string _goalSummary = string.Empty;

    [ObservableProperty]
    private decimal _identifiedPotential;

    [ObservableProperty]
    private string _historyText = string.Empty;

    [ObservableProperty]
    private decimal _currentMonthly;

    [ObservableProperty]
    private decimal _monthlySavings;

    [ObservableProperty]
    private decimal _annualSavings;

    [ObservableProperty]
    private decimal _projectedMonthly;

    public override string Title => "Estratégia";

    public ObservableCollection<OpportunityStepItem> Opportunities { get; } = [];

    public ObservableCollection<ScenarioRowViewModel> Scenario { get; } = [];

    public bool HasOpportunities => Opportunities.Count > 0;

    public bool HasScenario => Scenario.Count > 0;

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    [RelayCommand]
    private Task SaveGoalAsync() => RunAsync(async () =>
    {
        var name = GoalName;
        var target = GoalTarget ?? 0m;
        await UseCases.RunAsync<StrategyService>((s, ct) => s.SetGoalAsync(name, target, ct));
        StatusMessage = $"Meta de economia de {Format.Money(target)}/mês salva.";
        await ReloadAsync();
    });

    [RelayCommand]
    private Task ClearGoalAsync() => RunAsync(async () =>
    {
        await UseCases.RunAsync<StrategyService>((s, ct) => s.ClearGoalAsync(ct));
        StatusMessage = "Meta removida.";
        await ReloadAsync();
    });

    [RelayCommand]
    private Task SimulateAsync() => RunAsync(SimulateCoreAsync);

    [RelayCommand]
    private Task ResetScenarioAsync()
    {
        foreach (var row in Scenario)
        {
            row.ReductionPercent = 0;
        }

        return SimulateAsync();
    }

    private async Task ReloadAsync()
    {
        var strategy = await UseCases.RunAsync<StrategyService, StrategyDto>((s, ct) => s.GetAsync(ct));

        HasGoal = strategy.Goal is not null;
        if (strategy.Goal is { } goal)
        {
            GoalName = goal.Name;
            GoalTarget = goal.MonthlyTarget;
            GoalSummary = strategy.Gap == 0
                ? $"Objetivo: {Format.Money(goal.MonthlyTarget)}/mês ({Format.Money(goal.AnnualTarget)}/ano). As possíveis oportunidades identificadas somam {Format.Money(strategy.IdentifiedPotential)}/mês e alcançam o objetivo."
                : $"Objetivo: {Format.Money(goal.MonthlyTarget)}/mês ({Format.Money(goal.AnnualTarget)}/ano). As possíveis oportunidades identificadas somam {Format.Money(strategy.IdentifiedPotential)}/mês; faltam {Format.Money(strategy.Gap ?? 0)}/mês. Use o simulador para explorar outras reduções.";
        }
        else
        {
            GoalSummary = "Defina um objetivo mensal de economia para compará-lo com as oportunidades identificadas.";
        }

        IdentifiedPotential = strategy.IdentifiedPotential;
        HistoryText = strategy.HistoryMonths > 0
            ? $"Base: média dos {strategy.HistoryMonths} meses anteriores à fatura de {Format.LongMonth(strategy.ReferenceMonth).ToLowerInvariant()}."
            : $"Sem histórico anterior: a base é a fatura de {Format.LongMonth(strategy.ReferenceMonth).ToLowerInvariant()}.";

        Opportunities.Clear();
        foreach (var o in strategy.Opportunities)
        {
            Opportunities.Add(new OpportunityStepItem(o.Title, o.Detail, o.MonthlyPotential, o.Cumulative, o.ReachesGoal));
        }

        var previous = Scenario.ToDictionary(r => r.Baseline.CategoryId ?? Guid.Empty, r => r.ReductionPercent);
        Scenario.Clear();
        foreach (var baseline in strategy.Baselines)
        {
            Scenario.Add(new ScenarioRowViewModel(baseline) { ReductionPercent = previous.GetValueOrDefault(baseline.CategoryId ?? Guid.Empty) ?? 0 });
        }

        OnPropertyChanged(nameof(HasOpportunities));
        OnPropertyChanged(nameof(HasScenario));
        await SimulateCoreAsync();
    }

    /// <summary>Recalcula o cenário sem abrir outra operação de "ocupado" (já dentro de <see cref="ReloadAsync"/>).</summary>
    private async Task SimulateCoreAsync()
    {
        var reductions = Scenario
            .Where(r => r.Baseline.CategoryId is not null && r.ReductionPercent > 0)
            .ToDictionary(r => r.Baseline.CategoryId!.Value, r => Math.Clamp(r.ReductionPercent ?? 0, 0, 100) / 100m);
        var result = await UseCases.RunAsync<StrategyService, ScenarioResultDto>((s, ct) => s.SimulateAsync(reductions, ct));
        foreach (var row in Scenario)
        {
            row.Savings = result.Lines.FirstOrDefault(l => l.CategoryId == row.Baseline.CategoryId)?.MonthlySavings ?? 0m;
        }

        CurrentMonthly = result.CurrentMonthly;
        MonthlySavings = result.MonthlySavings;
        AnnualSavings = result.AnnualSavings;
        ProjectedMonthly = result.ProjectedMonthly;
    }
}
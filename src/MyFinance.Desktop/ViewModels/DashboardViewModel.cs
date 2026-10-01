using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Dashboard;
using MyFinance.Desktop.Controls;
using MyFinance.Desktop.Services;
using MyFinance.Domain.Analysis;

namespace MyFinance.Desktop.ViewModels;

/// <param name="BarPercent">Comprimento da barra em relação à maior categoria (0–100).</param>
/// <param name="Comparison">Ex.: "+32% vs média"; vazio sem base de comparação.</param>
public sealed record CategoryBarItem(string Name, string Color, decimal Amount, string ShareText, double BarPercent, string Comparison, bool IsUp);

public sealed record InvoiceItem(string CardName, decimal Amount, DateOnly DueDate);

/// <param name="Detail">Ex.: "R$ 620,00 → R$ 980,00".</param>
public sealed record VariationItem(string Name, string DifferenceText, string PercentText, string Detail);

public sealed record NextInvoiceItem(string MonthText, decimal Total, decimal Installments);

public sealed record InsightItem(string Message, bool IsAttention, bool IsPositive)
{
    public bool IsNeutral => !IsAttention && !IsPositive;
}

public sealed record OpportunityItem(string Title, string Detail, decimal MonthlyPotential);

/// <summary>"Como estou gastando meu dinheiro?" — a primeira tela.</summary>
public sealed partial class DashboardViewModel(PageServices services) : PageViewModel(services)
{
    private static readonly Option<ComparisonBase> VersusPrevious = new(ComparisonBase.PreviousMonth, "vs mês anterior");
    private static readonly Option<ComparisonBase> VersusAverage = new(ComparisonBase.Average, "vs média dos últimos meses");

    private static readonly Option<int>[] Periods =
        [new(3, "Últimos 3 meses"), new(6, "Últimos 6 meses"), new(12, "Últimos 12 meses")];

    private DashboardDto? _data;
    private DateOnly? _month;

    [ObservableProperty]
    private string _monthLabel = string.Empty;

    [ObservableProperty]
    private bool _isPartial;

    [ObservableProperty]
    private decimal _monthSpending;

    [ObservableProperty]
    private string _averageText = "—";

    [ObservableProperty]
    private string _averageCaption = string.Empty;

    [ObservableProperty]
    private string _variationText = "—";

    [ObservableProperty]
    private bool _variationUp;

    [ObservableProperty]
    private bool _variationDown;

    [ObservableProperty]
    private decimal _currentInvoiceTotal;

    [ObservableProperty]
    private decimal _outstandingInstallments;

    [ObservableProperty]
    private IReadOnlyList<ColumnItem> _evolution = [];

    [ObservableProperty]
    private IReadOnlyList<ColumnItem> _nextInvoicesChart = [];

    [ObservableProperty]
    private bool _hasSpending;

    [ObservableProperty]
    private string? _goalText;

    // Nuláveis: o ComboBox grava null na propriedade quando sua lista de itens é recarregada.
    [ObservableProperty]
    private Option<ComparisonBase>? _comparison = VersusAverage;

    [ObservableProperty]
    private Option<int>? _evolutionPeriod = Periods[1];

    public override string Title => "Dashboard";

    public IReadOnlyList<Option<ComparisonBase>> ComparisonOptions { get; } = [VersusPrevious, VersusAverage];

    public IReadOnlyList<Option<int>> EvolutionPeriods => Periods;

    public ObservableCollection<CategoryBarItem> Categories { get; } = [];

    public ObservableCollection<InvoiceItem> Invoices { get; } = [];

    public ObservableCollection<VariationItem> Increased { get; } = [];

    public ObservableCollection<VariationItem> Decreased { get; } = [];

    public ObservableCollection<NextInvoiceItem> NextInvoices { get; } = [];

    public ObservableCollection<OpportunityItem> Opportunities { get; } = [];

    public ObservableCollection<InsightItem> Insights { get; } = [];

    public bool HasIncreased => Increased.Count > 0;

    public bool HasDecreased => Decreased.Count > 0;

    public bool HasOpportunities => Opportunities.Count > 0;

    public bool HasInsights => Insights.Count > 0;

    public override Task LoadAsync() => RefreshAsync();

    partial void OnComparisonChanged(Option<ComparisonBase>? value)
    {
        if (_data is not null && value is not null)
        {
            ApplyComparison(_data);
        }
    }

    partial void OnEvolutionPeriodChanged(Option<int>? value)
    {
        if (_data is not null && value is not null)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var months = EvolutionPeriod?.Value ?? DashboardService.DefaultEvolutionMonths;
        var month = _month;
        var data = await UseCases.RunAsync<DashboardService, DashboardDto>((s, ct) => s.GetAsync(month, months, ct));
        Apply(data);
    });

    [RelayCommand]
    private Task PreviousMonthAsync()
    {
        _month = (_data?.ReferenceMonth ?? DateOnly.FromDateTime(DateTime.Today)).AddMonths(-1);
        return RefreshAsync();
    }

    [RelayCommand]
    private Task NextMonthAsync()
    {
        _month = (_data?.ReferenceMonth ?? DateOnly.FromDateTime(DateTime.Today)).AddMonths(1);
        return RefreshAsync();
    }

    [RelayCommand]
    private Task CurrentMonthAsync()
    {
        _month = null;
        return RefreshAsync();
    }

    private void Apply(DashboardDto data)
    {
        _data = data;
        var culture = CultureInfo.CurrentCulture;
        var month = data.ReferenceMonth.ToString("MMMM 'de' yyyy", culture);
        MonthLabel = "Fatura de " + month;
        IsPartial = data.IsPartial;

        MonthSpending = data.MonthSpending;
        AverageText = data.Average is { } average ? Format.Money(average) : "—";
        AverageCaption = data.HistoryMonths > 0 ? $"média dos {data.HistoryMonths} meses anteriores" : "sem histórico anterior";
        VariationText = data.Variation is { } variation ? Format.SignedPercent(variation) : "—";
        VariationUp = data.Variation > 0;
        VariationDown = data.Variation < 0;
        CurrentInvoiceTotal = data.CurrentInvoiceTotal;
        OutstandingInstallments = data.OutstandingInstallments;

        Invoices.Clear();
        foreach (var invoice in data.OpenInvoices)
        {
            Invoices.Add(new InvoiceItem(invoice.CreditCardName, invoice.Invoice.Amount, invoice.Invoice.DueDate));
        }

        Evolution = [.. data.Evolution.Select(m => new ColumnItem(
            Format.ShortMonth(m.Month), (double)m.Amount, 0, $"{Format.LongMonth(m.Month)}: {Format.Money(m.Amount)}",
            Highlight: m.Month == data.ReferenceMonth))];

        NextInvoices.Clear();
        foreach (var next in data.NextInvoices)
        {
            NextInvoices.Add(new NextInvoiceItem(Format.LongMonth(next.Month), next.Total, next.Installments));
        }

        NextInvoicesChart = [.. data.NextInvoices.Select(n => new ColumnItem(
            Format.ShortMonth(n.Month), (double)n.Posted, (double)n.Installments,
            $"{Format.LongMonth(n.Month)}\nLançado: {Format.Money(n.Posted)}\nParcelas previstas: {Format.Money(n.Installments)}\nTotal: {Format.Money(n.Total)}"))];

        Opportunities.Clear();
        foreach (var opportunity in data.Opportunities)
        {
            Opportunities.Add(new OpportunityItem(opportunity.Title, opportunity.Detail, opportunity.MonthlyPotential));
        }

        GoalText = data.Goal is { } goal
            ? $"Meta de economia: {Format.Money(goal.MonthlyTarget)}/mês · potencial identificado: {Format.Money(goal.IdentifiedPotential)}/mês"
            : null;

        Insights.Clear();
        foreach (var insight in data.Insights)
        {
            Insights.Add(new InsightItem(insight.Message, insight.Tone == InsightTone.Attention, insight.Tone == InsightTone.Positive));
        }

        ApplyComparison(data);
        OnPropertyChanged(nameof(HasOpportunities));
        OnPropertyChanged(nameof(HasInsights));
    }

    /// <summary>Barras por categoria e "o que aumentou/diminuiu" conforme a base de comparação escolhida.</summary>
    private void ApplyComparison(DashboardDto data)
    {
        var versusAverage = Comparison?.Value != ComparisonBase.PreviousMonth;
        var label = versusAverage ? "vs média" : "vs mês anterior";

        Categories.Clear();
        var max = data.Categories.Count == 0 ? 0 : data.Categories.Max(c => c.Amount);
        foreach (var category in data.Categories)
        {
            var change = versusAverage ? category.VersusAverage : category.VersusPreviousMonth;
            Categories.Add(new CategoryBarItem(
                category.Name,
                category.Color,
                category.Amount,
                category.Percent.ToString("P0", CultureInfo.CurrentCulture),
                max == 0 ? 0 : (double)(category.Amount / max * 100),
                change is { } c ? $"{Format.SignedPercent(c)} {label}" : string.Empty,
                change > 0));
        }

        HasSpending = Categories.Count > 0;

        Fill(Increased, versusAverage ? data.IncreasedVsAverage : data.IncreasedVsPreviousMonth);
        Fill(Decreased, versusAverage ? data.DecreasedVsAverage : data.DecreasedVsPreviousMonth);
        OnPropertyChanged(nameof(HasIncreased));
        OnPropertyChanged(nameof(HasDecreased));
    }

    private static void Fill(ObservableCollection<VariationItem> target, IReadOnlyList<VariationDto> source)
    {
        target.Clear();
        foreach (var v in source.Take(5))
        {
            target.Add(new VariationItem(
                v.Name,
                (v.Difference > 0 ? "+" : "−") + Format.Money(Math.Abs(v.Difference)),
                v.Percent is { } p ? Format.SignedPercent(p) : "novo",
                $"{Format.Money(v.Baseline)} → {Format.Money(v.Current)}"));
        }
    }
}
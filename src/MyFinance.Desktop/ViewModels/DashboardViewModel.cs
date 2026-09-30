using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Dashboard;
using MyFinance.Desktop.Controls;

namespace MyFinance.Desktop.ViewModels;

public sealed record CategoryBarItem(string Name, string Color, decimal Amount, double Percent);

public sealed record InvoiceItem(string CardName, decimal Amount, DateOnly DueDate);

public sealed partial class DashboardViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    private string _monthLabel = string.Empty;

    [ObservableProperty]
    private decimal _totalBalance;

    [ObservableProperty]
    private decimal _monthIncome;

    [ObservableProperty]
    private decimal _monthExpenses;

    [ObservableProperty]
    private decimal _monthBalance;

    [ObservableProperty]
    private decimal _currentInvoiceTotal;

    [ObservableProperty]
    private IReadOnlyList<BarGroup> _incomeVsExpenses = [];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _balanceEvolution = [];

    [ObservableProperty]
    private bool _hasExpenses;

    public override string Title => "Dashboard";

    public ObservableCollection<CategoryBarItem> ExpenseCategories { get; } = [];

    public ObservableCollection<InvoiceItem> Invoices { get; } = [];

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var data = await UseCases.RunAsync<DashboardService, DashboardDto>((s, ct) => s.GetAsync(ct));
        Apply(data);
    });

    private void Apply(DashboardDto data)
    {
        var culture = CultureInfo.CurrentCulture;
        var month = data.ReferenceMonth.ToString("MMMM 'de' yyyy", culture);
        MonthLabel = culture.TextInfo.ToUpper(month[0]) + month[1..];

        TotalBalance = data.TotalBalance;
        MonthIncome = data.MonthIncome;
        MonthExpenses = data.MonthExpenses;
        MonthBalance = data.MonthBalance;
        CurrentInvoiceTotal = data.CurrentInvoiceTotal;

        ExpenseCategories.Clear();
        var max = data.ExpensesByCategory.Count == 0 ? 0 : data.ExpensesByCategory.Max(c => c.Amount);
        foreach (var category in data.ExpensesByCategory)
        {
            ExpenseCategories.Add(new CategoryBarItem(category.CategoryName, category.Color, category.Amount,
                max == 0 ? 0 : (double)(category.Amount / max * 100)));
        }

        HasExpenses = ExpenseCategories.Count > 0;

        Invoices.Clear();
        foreach (var invoice in data.Invoices)
        {
            Invoices.Add(new InvoiceItem(invoice.CreditCardName, invoice.Invoice.Amount, invoice.Invoice.DueDate));
        }

        IncomeVsExpenses = [.. data.IncomeVsExpenses.Select(m =>
            new BarGroup(m.Month.ToString("MMM/yy", culture), (double)m.Income, (double)m.Expenses))];
        BalanceEvolution = [.. data.BalanceEvolution.Select(p =>
            new ChartPoint(p.Date.ToString("MMM/yy", culture), (double)p.Balance))];
    }
}
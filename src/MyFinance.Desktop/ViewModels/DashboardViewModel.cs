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
    private decimal _monthSpending;

    [ObservableProperty]
    private decimal _currentInvoiceTotal;

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _monthlySpending = [];

    [ObservableProperty]
    private bool _hasSpending;

    public override string Title => "Dashboard";

    public ObservableCollection<CategoryBarItem> SpendingCategories { get; } = [];

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
        MonthLabel = "Fatura de " + month;

        MonthSpending = data.MonthSpending;
        CurrentInvoiceTotal = data.CurrentInvoiceTotal;

        SpendingCategories.Clear();
        var max = data.SpendingByCategory.Count == 0 ? 0 : data.SpendingByCategory.Max(c => c.Amount);
        foreach (var category in data.SpendingByCategory)
        {
            SpendingCategories.Add(new CategoryBarItem(category.CategoryName, category.Color, category.Amount,
                max == 0 ? 0 : (double)(category.Amount / max * 100)));
        }

        HasSpending = SpendingCategories.Count > 0;

        Invoices.Clear();
        foreach (var invoice in data.Invoices)
        {
            Invoices.Add(new InvoiceItem(invoice.CreditCardName, invoice.Invoice.Amount, invoice.Invoice.DueDate));
        }

        MonthlySpending = [.. data.MonthlySpending.Select(m => new ChartPoint(m.Month.ToString("MMM/yy", culture), (double)m.Amount))];
    }
}
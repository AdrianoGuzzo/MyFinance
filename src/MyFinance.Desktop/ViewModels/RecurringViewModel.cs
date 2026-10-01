using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Strategy;
using MyFinance.Desktop.Services;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

/// <summary>Linha de gasto recorrente; trocar a classificação grava imediatamente.</summary>
public sealed partial class RecurringRowViewModel : ViewModelBase
{
    private readonly Func<RecurringRowViewModel, RecurringClassification, Task> _onClassified;
    private readonly bool _initialized;

    [ObservableProperty]
    private Option<RecurringClassification>? _classification;

    public RecurringRowViewModel(RecurringExpenseDto item, Func<RecurringRowViewModel, RecurringClassification, Task> onClassified)
    {
        Item = item;
        _onClassified = onClassified;
        _classification = Options.RecurringClassifications.First(c => c.Value == item.Classification);
        _initialized = true;
    }

    public RecurringExpenseDto Item { get; }

    public IReadOnlyList<Option<RecurringClassification>> ClassificationOptions => Options.RecurringClassifications;

    public bool IsActive => !Item.IsDismissed;

    public string SeenText => $"{Item.MonthsSeen} meses · último em {Format.ShortMonth(Item.LastSeenMonth)}";

    partial void OnClassificationChanged(Option<RecurringClassification>? oldValue, Option<RecurringClassification>? newValue)
    {
        if (_initialized && newValue is not null && oldValue is not null && newValue.Value != oldValue.Value)
        {
            _ = _onClassified(this, newValue.Value);
        }
    }
}

/// <summary>Gastos recorrentes (assinaturas, academia...) detectados automaticamente no histórico.</summary>
public sealed partial class RecurringViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    private bool _showDismissed;

    [ObservableProperty]
    private decimal _monthlyTotal;

    [ObservableProperty]
    private decimal _annualTotal;

    [ObservableProperty]
    private decimal _optionalMonthly;

    public override string Title => "Gastos recorrentes";

    public ObservableCollection<RecurringRowViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnShowDismissedChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    private Task DismissAsync(RecurringRowViewModel row) => ChangeAsync(
        (s, ct) => s.DismissAsync(row.Item.Id, ct), $"\"{row.Item.Name}\" não será mais tratado como recorrente.");

    [RelayCommand]
    private Task RestoreAsync(RecurringRowViewModel row) => ChangeAsync(
        (s, ct) => s.RestoreAsync(row.Item.Id, ct), $"\"{row.Item.Name}\" voltou para a lista de recorrentes.");

    private Task ClassifyAsync(RecurringRowViewModel row, RecurringClassification classification) => ChangeAsync(
        (s, ct) => s.ClassifyAsync(row.Item.Id, classification, ct), $"\"{row.Item.Name}\" classificado como {Labels.For(classification)}.");

    private Task ChangeAsync(Func<RecurringExpenseService, CancellationToken, Task> change, string done) => RunAsync(async () =>
    {
        await UseCases.RunAsync<RecurringExpenseService>(change);
        StatusMessage = done;
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        var include = ShowDismissed;
        var overview = await UseCases.RunAsync<RecurringExpenseService, RecurringOverviewDto>((s, ct) => s.GetAsync(include, ct));

        Items.Clear();
        foreach (var item in overview.Items)
        {
            Items.Add(new RecurringRowViewModel(item, ClassifyAsync));
        }

        MonthlyTotal = overview.MonthlyTotal;
        AnnualTotal = overview.AnnualTotal;
        OptionalMonthly = overview.OptionalMonthly;
        OnPropertyChanged(nameof(IsEmpty));
    }
}
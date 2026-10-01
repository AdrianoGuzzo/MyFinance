using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Categories;
using MyFinance.Application.Strategy;
using MyFinance.Desktop.Services;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

public sealed class LimitRowViewModel(SpendingLimitDto limit)
{
    public SpendingLimitDto Limit { get; } = limit;

    /// <summary>Barra limitada a 100%; o excedente aparece no texto.</summary>
    public double BarPercent => (double)Math.Min(1m, Limit.Percent) * 100;

    public string PercentText => Limit.Percent.ToString("P0", CultureInfo.CurrentCulture);

    /// <summary>Situação com símbolo e texto (nunca só a cor).</summary>
    public string StatusText => Limit.Status switch
    {
        LimitStatus.Exceeded => "✕ " + Labels.For(Limit.Status),
        LimitStatus.Near => "! " + Labels.For(Limit.Status),
        _ => "✓ " + Labels.For(Limit.Status),
    };

    /// <summary>"Disponível: R$ 180,00" ou "Excedente: R$ 120,00".</summary>
    public string BalanceText => Limit.Status == LimitStatus.Exceeded
        ? $"Excedente: {Format.Money(Limit.Excess)}"
        : $"Disponível: {Format.Money(Limit.Available)}";

    public bool IsNear => Limit.Status == LimitStatus.Near;

    public bool IsExceeded => Limit.Status == LimitStatus.Exceeded;
}

/// <summary>Limites mensais de gastos por categoria e seu consumo na fatura atual.</summary>
public sealed partial class LimitsViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsEditing))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand), nameof(ActivateCommand), nameof(DeleteCommand))]
    private LimitRowViewModel? _selected;

    // Nulável: o ComboBox grava null na propriedade quando sua lista de itens é recarregada.
    [ObservableProperty]
    private CategoryOption? _category;

    [ObservableProperty]
    private decimal? _amount = 500m;

    [ObservableProperty]
    private string _monthLabel = string.Empty;

    public override string Title => "Limites";

    public ObservableCollection<LimitRowViewModel> Limits { get; } = [];

    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];

    public bool IsEditing => Selected is not null;

    public bool IsEmpty => Limits.Count == 0;

    public string FormTitle => Selected is null ? "Novo limite" : $"Editar: {Selected.Limit.CategoryName}";

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnSelectedChanged(LimitRowViewModel? value)
    {
        Category = CategoryOptions.FirstOrDefault(c => c.Id == value?.Limit.CategoryId);
        Amount = value?.Limit.MonthlyAmount ?? 500m;
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var amount = Amount ?? 0m;
        if (Selected is { } current)
        {
            await UseCases.RunAsync<SpendingLimitService>((s, ct) => s.ChangeAmountAsync(current.Limit.Id, amount, ct));
        }
        else if (Category?.Id is { } categoryId)
        {
            await UseCases.RunAsync<SpendingLimitService, Guid>((s, ct) => s.CreateAsync(new SaveSpendingLimitCommand(categoryId, amount), ct));
        }
        else
        {
            await Dialogs.ShowMessageAsync("Categoria obrigatória", "Escolha a categoria do limite.");
            return;
        }

        StatusMessage = $"Limite de {Format.Money(amount)}/mês salvo.";
        Selected = null;
        await ReloadAsync();
    });

    private bool CanDeactivate() => Selected is { Limit.IsActive: true };

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private Task DeactivateAsync() => ChangeAsync((s, id, ct) => s.DeactivateAsync(id, ct), "desativado");

    private bool CanActivate() => Selected is { Limit.IsActive: false };

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private Task ActivateAsync() => ChangeAsync((s, id, ct) => s.ActivateAsync(id, ct), "reativado");

    private bool CanDelete() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var name = Selected!.Limit.CategoryName;
        if (await Dialogs.ConfirmAsync("Excluir limite", $"Excluir o limite de \"{name}\"?", "Excluir", isDestructive: true))
        {
            await ChangeAsync((s, id, ct) => s.DeleteAsync(id, ct), "excluído");
        }
    }

    private Task ChangeAsync(Func<SpendingLimitService, Guid, CancellationToken, Task> change, string done) => RunAsync(async () =>
    {
        var limit = Selected!.Limit;
        await UseCases.RunAsync<SpendingLimitService>((s, ct) => change(s, limit.Id, ct));
        StatusMessage = $"Limite de \"{limit.CategoryName}\" {done}.";
        Selected = null;
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        var categories = await UseCases.RunAsync<CategoryService, IReadOnlyList<CategoryDto>>((s, ct) => s.ListAsync(false, ct));
        CategoryOptions.Clear();
        foreach (var category in categories)
        {
            CategoryOptions.Add(new CategoryOption(category.Id, category.FullName));
        }

        var limits = await UseCases.RunAsync<SpendingLimitService, IReadOnlyList<SpendingLimitDto>>((s, ct) => s.ListAsync(null, ct));
        Limits.Clear();
        foreach (var limit in limits)
        {
            Limits.Add(new LimitRowViewModel(limit));
        }

        var month = await UseCases.RunAsync<Application.Analysis.AnalysisLoader, DateOnly>((s, ct) => s.DefaultMonthAsync(ct));
        MonthLabel = "Consumo na fatura de " + Format.LongMonth(month).ToLower(CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(IsEmpty));
    }
}
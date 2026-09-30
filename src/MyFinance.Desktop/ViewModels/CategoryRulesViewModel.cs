using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Categories;

namespace MyFinance.Desktop.ViewModels;

/// <summary>Regras de categorização automática (aba da tela de Categorias).</summary>
public sealed partial class CategoryRulesViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsEditing))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand), nameof(ActivateCommand), nameof(DeleteCommand))]
    private CategoryRuleDto? _selected;

    [ObservableProperty]
    private string _pattern = string.Empty;

    // Nulável: o ComboBox grava null na propriedade quando sua lista de itens é recarregada.
    [ObservableProperty]
    private CategoryOption? _category;

    [ObservableProperty]
    private decimal? _priority = 0;

    public override string Title => "Regras de categorização";

    public ObservableCollection<CategoryRuleDto> Rules { get; } = [];

    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];

    public bool IsEditing => Selected is not null;

    public string FormTitle => Selected is null ? "Nova regra" : $"Editar: {Selected.Pattern}";

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnSelectedChanged(CategoryRuleDto? value)
    {
        Pattern = value?.Pattern ?? string.Empty;
        Category = CategoryOptions.FirstOrDefault(c => c.Id == value?.CategoryId);
        Priority = value?.Priority ?? 0;
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
        if (Category?.Id is not { } categoryId)
        {
            await Dialogs.ShowMessageAsync("Categoria obrigatória", "Escolha a categoria aplicada pela regra.");
            return;
        }

        var command = new SaveCategoryRuleCommand(Pattern, categoryId, (int)(Priority ?? 0));
        if (Selected is { } current)
        {
            await UseCases.RunAsync<CategoryRuleService>((s, ct) => s.UpdateAsync(current.Id, command, ct));
        }
        else
        {
            await UseCases.RunAsync<CategoryRuleService, Guid>((s, ct) => s.CreateAsync(command, ct));
        }

        StatusMessage = $"Regra \"{Pattern.Trim()}\" salva.";
        Selected = null;
        await ReloadAsync();
    });

    [RelayCommand]
    private Task ApplyAsync() => RunAsync(async () =>
    {
        var count = await UseCases.RunAsync<CategoryRuleService, int>((s, ct) => s.ApplyToUncategorizedAsync(ct));
        StatusMessage = count == 0
            ? "Nenhum lançamento sem categoria corresponde às regras."
            : $"{count} lançamento(s) sem categoria foram categorizados.";
    }, "Aplicando regras...");

    private bool CanDeactivate() => Selected is { IsActive: true };

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private Task DeactivateAsync() => ChangeAsync((s, id, ct) => s.DeactivateAsync(id, ct), "desativada");

    private bool CanActivate() => Selected is { IsActive: false };

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private Task ActivateAsync() => ChangeAsync((s, id, ct) => s.ActivateAsync(id, ct), "reativada");

    private bool CanDelete() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var rule = Selected!;
        if (await Dialogs.ConfirmAsync("Excluir regra", $"Excluir a regra \"{rule.Pattern}\"?\n\nLançamentos já categorizados não são alterados.",
            "Excluir", isDestructive: true))
        {
            await ChangeAsync((s, id, ct) => s.DeleteAsync(id, ct), "excluída");
        }
    }

    private Task ChangeAsync(Func<CategoryRuleService, Guid, CancellationToken, Task> change, string done) => RunAsync(async () =>
    {
        var rule = Selected!;
        await UseCases.RunAsync<CategoryRuleService>((s, ct) => change(s, rule.Id, ct));
        StatusMessage = $"Regra \"{rule.Pattern}\" {done}.";
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

        var rules = await UseCases.RunAsync<CategoryRuleService, IReadOnlyList<CategoryRuleDto>>((s, ct) => s.ListAsync(ct));
        Rules.Clear();
        foreach (var rule in rules)
        {
            Rules.Add(rule);
        }
    }
}
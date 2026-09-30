using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Categories;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class CategoriesViewModel(PageServices services) : PageViewModel(services)
{
    private const string DefaultColor = "#95A5A6";
    private static readonly CategoryOption NoParent = new(null, "(nenhuma — categoria principal)");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsEditing))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand), nameof(ActivateCommand))]
    private CategoryDto? _selected;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _color = DefaultColor;

    // Nulável: o ComboBox grava null na propriedade quando sua lista de itens é recarregada.
    [ObservableProperty]
    private CategoryOption? _parent = NoParent;

    public override string Title => "Categorias";

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    public ObservableCollection<CategoryOption> ParentOptions { get; } = [NoParent];

    public bool IsEditing => Selected is not null;

    public string FormTitle => Selected is null ? "Nova categoria" : $"Editar: {Selected.FullName}";

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync();

    partial void OnSelectedChanged(CategoryDto? value)
    {
        Name = value?.Name ?? string.Empty;
        Color = value?.Color ?? DefaultColor;
        Parent = ParentOptions.FirstOrDefault(p => p.Id == value?.ParentCategoryId) ?? NoParent;
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
        if (Selected is { } current)
        {
            await UseCases.RunAsync<CategoryService>((s, ct) => s.UpdateAsync(current.Id, new UpdateCategoryCommand(Name, Color), ct));
        }
        else
        {
            var command = new CreateCategoryCommand(Name, Color, Parent?.Id);
            await UseCases.RunAsync<CategoryService, Guid>((s, ct) => s.CreateAsync(command, ct));
        }

        StatusMessage = $"Categoria \"{Name.Trim()}\" salva.";
        Selected = null;
        await ReloadAsync();
    });

    private bool CanDeactivate() => Selected is { IsActive: true };

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private async Task DeactivateAsync()
    {
        var category = Selected!;
        var children = Categories.Count(c => c.ParentCategoryId == category.Id && c.IsActive);
        var detail = children > 0 ? $"\n\nSuas {children} subcategoria(s) também serão desativadas." : string.Empty;

        var confirmed = await Dialogs.ConfirmAsync(
            "Desativar categoria",
            $"Desativar \"{category.FullName}\"?{detail}\n\nLançamentos já categorizados são mantidos.",
            "Desativar",
            isDestructive: true);

        if (confirmed && await RunAsync(() => UseCases.RunAsync<CategoryService>((s, ct) => s.DeactivateAsync(category.Id, ct))))
        {
            StatusMessage = $"Categoria \"{category.FullName}\" desativada.";
            Selected = null;
            await RunAsync(ReloadAsync);
        }
    }

    private bool CanActivate() => Selected is { IsActive: false };

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private Task ActivateAsync() => RunAsync(async () =>
    {
        var category = Selected!;
        await UseCases.RunAsync<CategoryService>((s, ct) => s.ActivateAsync(category.Id, ct));
        StatusMessage = $"Categoria \"{category.FullName}\" reativada.";
        Selected = null;
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        var list = await UseCases.RunAsync<CategoryService, IReadOnlyList<CategoryDto>>((s, ct) => s.ListAsync(ShowInactive, ct));

        Categories.Clear();
        foreach (var category in list)
        {
            Categories.Add(category);
        }

        ParentOptions.Clear();
        ParentOptions.Add(NoParent);
        foreach (var root in list.Where(c => c.ParentCategoryId is null && c.IsActive))
        {
            ParentOptions.Add(new CategoryOption(root.Id, root.Name));
        }

        Parent = NoParent;
    }
}
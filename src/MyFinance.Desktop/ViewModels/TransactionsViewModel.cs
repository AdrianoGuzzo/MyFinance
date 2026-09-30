using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Accounts;
using MyFinance.Application.Categories;
using MyFinance.Application.Common;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

/// <summary>Linha da lista; trocar a categoria no ComboBox categoriza o lançamento imediatamente.</summary>
public sealed partial class TransactionRowViewModel : ViewModelBase
{
    private readonly Func<TransactionRowViewModel, CategoryOption, Task> _onCategoryChanged;
    private readonly bool _initialized;

    [ObservableProperty]
    private CategoryOption? _selectedCategory;

    public TransactionRowViewModel(
        TransactionListItem item,
        IReadOnlyList<CategoryOption> categoryOptions,
        Func<TransactionRowViewModel, CategoryOption, Task> onCategoryChanged)
    {
        Item = item;
        _onCategoryChanged = onCategoryChanged;

        // Categoria desativada não está entre as opções ativas: é exibida marcada, em vez de parecer "sem categoria".
        var current = categoryOptions.FirstOrDefault(o => o.Id == item.CategoryId);
        if (current is null && item.CategoryId is { } inactiveId)
        {
            current = new CategoryOption(inactiveId, $"{item.CategoryName} (desativada)");
            categoryOptions = [.. categoryOptions, current];
        }

        CategoryOptions = categoryOptions;
        _selectedCategory = current;
        _initialized = true;
    }

    public TransactionListItem Item { get; }

    public IReadOnlyList<CategoryOption> CategoryOptions { get; }

    public bool IsExpense => Item.TransactionType == TransactionType.Expense;

    public string OwnerLabel => Item.OwnerType == TransactionOwnerType.CreditCard ? $"Cartão · {Item.OwnerName}" : Item.OwnerName;

    partial void OnSelectedCategoryChanged(CategoryOption? oldValue, CategoryOption? newValue)
    {
        if (_initialized && newValue is not null && newValue.Id != oldValue?.Id)
        {
            _ = _onCategoryChanged(this, newValue);
        }
    }
}

public sealed record OwnerFilterOption(string Label, Guid? AccountId, Guid? CreditCardId)
{
    public override string ToString() => Label;
}

public sealed record CategoryFilterOption(string Label, Guid? CategoryId, bool UncategorizedOnly)
{
    public override string ToString() => Label;
}

public sealed partial class TransactionsViewModel(PageServices services) : PageViewModel(services)
{
    private const int PageSize = 50;
    private static readonly OwnerFilterOption AllOwners = new("Todas as contas e cartões", null, null);
    private static readonly CategoryFilterOption AllCategories = new("Todas as categorias", null, false);

    private IReadOnlyList<CategoryOption> _categoryOptions = [];

    // Nuláveis: os ComboBoxes gravam null enquanto as listas de opções são recarregadas.
    [ObservableProperty]
    private OwnerFilterOption? _selectedOwner = AllOwners;

    [ObservableProperty]
    private CategoryFilterOption? _selectedCategoryFilter = AllCategories;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    private int _page = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    private int _totalPages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private int _totalCount;

    public override string Title => "Transações";

    public ObservableCollection<TransactionRowViewModel> Rows { get; } = [];

    public ObservableCollection<OwnerFilterOption> OwnerOptions { get; } = [AllOwners];

    public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; } = [AllCategories];

    public string PageLabel => TotalCount == 0
        ? "Nenhum lançamento encontrado"
        : $"{TotalCount} lançamento(s) · página {Page} de {TotalPages}";

    public override Task LoadAsync() => RunAsync(async () =>
    {
        await LoadFilterOptionsAsync();
        await SearchPageAsync();
    });

    [RelayCommand]
    private Task SearchAsync()
    {
        Page = 1;
        return RunAsync(SearchPageAsync);
    }

    [RelayCommand]
    private Task ClearFiltersAsync()
    {
        SelectedOwner = AllOwners;
        SelectedCategoryFilter = AllCategories;
        FromDate = null;
        ToDate = null;
        SearchText = null;
        return SearchAsync();
    }

    private bool CanGoPrevious() => Page > 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousPageAsync()
    {
        Page--;
        return RunAsync(SearchPageAsync);
    }

    private bool CanGoNext() => Page < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextPageAsync()
    {
        Page++;
        return RunAsync(SearchPageAsync);
    }

    private async Task CategorizeAsync(TransactionRowViewModel row, CategoryOption option)
    {
        var ok = await RunAsync(() => UseCases.RunAsync<TransactionService>((s, ct) => s.CategorizeAsync(row.Item.Id, option.Id, ct)));
        StatusMessage = ok
            ? $"\"{row.Item.Description}\" → {option.Label}"
            : null;

        if (!ok)
        {
            await RunAsync(SearchPageAsync);
        }
    }

    private async Task SearchPageAsync()
    {
        var search = new TransactionSearch
        {
            AccountId = SelectedOwner?.AccountId,
            CreditCardId = SelectedOwner?.CreditCardId,
            From = FromDate is { } from ? DateOnly.FromDateTime(from) : null,
            To = ToDate is { } to ? DateOnly.FromDateTime(to) : null,
            CategoryId = SelectedCategoryFilter?.CategoryId,
            UncategorizedOnly = SelectedCategoryFilter?.UncategorizedOnly ?? false,
            Text = SearchText,
            Page = Page,
            PageSize = PageSize,
        };

        var result = await UseCases.RunAsync<TransactionService, PagedResult<TransactionListItem>>((s, ct) => s.SearchAsync(search, ct));

        Rows.Clear();
        foreach (var item in result.Items)
        {
            Rows.Add(new TransactionRowViewModel(item, _categoryOptions, CategorizeAsync));
        }

        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
    }

    private async Task LoadFilterOptionsAsync()
    {
        var accounts = await UseCases.RunAsync<AccountService, IReadOnlyList<AccountDto>>((s, ct) => s.ListAsync(true, ct));
        var cards = await UseCases.RunAsync<CreditCardService, IReadOnlyList<CreditCardDto>>((s, ct) => s.ListAsync(true, ct));
        var categories = await UseCases.RunAsync<CategoryService, IReadOnlyList<CategoryDto>>((s, ct) => s.ListAsync(true, ct));

        var owner = SelectedOwner;
        OwnerOptions.Clear();
        OwnerOptions.Add(AllOwners);
        foreach (var account in accounts)
        {
            OwnerOptions.Add(new OwnerFilterOption($"Conta · {account.Name}", account.Id, null));
        }

        foreach (var card in cards)
        {
            OwnerOptions.Add(new OwnerFilterOption($"Cartão · {card.Name}", null, card.Id));
        }

        SelectedOwner = OwnerOptions.FirstOrDefault(o => o == owner) ?? AllOwners;

        var categoryFilter = SelectedCategoryFilter;
        CategoryFilterOptions.Clear();
        CategoryFilterOptions.Add(AllCategories);
        CategoryFilterOptions.Add(new CategoryFilterOption("Sem categoria", null, true));
        foreach (var category in categories.Where(c => c.ParentCategoryId is null))
        {
            CategoryFilterOptions.Add(new CategoryFilterOption(category.Name, category.Id, false));
        }

        SelectedCategoryFilter = CategoryFilterOptions.FirstOrDefault(o => o == categoryFilter) ?? AllCategories;

        _categoryOptions =
        [
            new CategoryOption(null, "(sem categoria)"),
            .. categories.Where(c => c.IsActive).Select(c => new CategoryOption(c.Id, c.FullName)),
        ];
    }
}
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Categories;
using MyFinance.Application.Common;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Transactions;
using MyFinance.Domain;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

/// <summary>Linha da lista; trocar a categoria ou o tipo no ComboBox altera o lançamento imediatamente.</summary>
public sealed partial class TransactionRowViewModel : ViewModelBase
{
    private readonly Func<TransactionRowViewModel, CategoryOption, Task> _onCategoryChanged;
    private readonly Func<TransactionRowViewModel, TransactionKind, Task> _onKindChanged;
    private readonly bool _initialized;

    [ObservableProperty]
    private CategoryOption? _selectedCategory;

    [ObservableProperty]
    private Option<TransactionKind>? _selectedKind;

    public TransactionRowViewModel(
        TransactionListItem item,
        IReadOnlyList<CategoryOption> categoryOptions,
        Func<TransactionRowViewModel, CategoryOption, Task> onCategoryChanged,
        Func<TransactionRowViewModel, TransactionKind, Task> onKindChanged)
    {
        Item = item;
        _onCategoryChanged = onCategoryChanged;
        _onKindChanged = onKindChanged;

        // Categoria desativada não está entre as opções ativas: é exibida marcada, em vez de parecer "sem categoria".
        var current = categoryOptions.FirstOrDefault(o => o.Id == item.CategoryId);
        if (current is null && item.CategoryId is { } inactiveId)
        {
            current = new CategoryOption(inactiveId, $"{item.CategoryName} (desativada)");
            categoryOptions = [.. categoryOptions, current];
        }

        CategoryOptions = categoryOptions;
        _selectedCategory = current;
        _selectedKind = KindOptions.First(k => k.Value == item.Kind);
        _initialized = true;
    }

    public TransactionListItem Item { get; }

    public IReadOnlyList<CategoryOption> CategoryOptions { get; }

    public IReadOnlyList<Option<TransactionKind>> KindOptions => Options.TransactionKinds;

    /// <summary>Estorno (reduz gastos) aparece em verde.</summary>
    public bool ReducesSpending => Item.SpendingAmount < 0;

    public bool IsPayment => Item.Kind == TransactionKind.Payment;

    public string? InstallmentLabel => Item.InstallmentNumber is { } n ? $"{n}/{Item.InstallmentCount}" : null;

    public string CardLabel => $"{Item.CreditCardName} · {Item.InvoiceMonth:MM/yyyy}";

    partial void OnSelectedCategoryChanged(CategoryOption? oldValue, CategoryOption? newValue)
    {
        if (_initialized && newValue is not null && newValue.Id != oldValue?.Id)
        {
            _ = _onCategoryChanged(this, newValue);
        }
    }

    partial void OnSelectedKindChanged(Option<TransactionKind>? oldValue, Option<TransactionKind>? newValue)
    {
        if (_initialized && newValue is not null && oldValue is not null && newValue.Value != oldValue.Value)
        {
            _ = _onKindChanged(this, newValue.Value);
        }
    }
}

public sealed record CategoryFilterOption(string Label, Guid? CategoryId, bool UncategorizedOnly)
{
    public override string ToString() => Label;
}

public sealed partial class TransactionsViewModel(PageServices services) : PageViewModel(services)
{
    private const int PageSize = 50;
    private static readonly CardOption AllCards = new(null, "Todos os cartões");
    private static readonly CategoryFilterOption AllCategories = new("Todas as categorias", null, false);
    private static readonly Option<TransactionKind?> AllKinds = new(null, "Todos os tipos");

    private IReadOnlyList<CategoryOption> _categoryOptions = [];

    // Nuláveis: os ComboBoxes gravam null enquanto as listas de opções são recarregadas.
    [ObservableProperty]
    private CardOption? _selectedCard = AllCards;

    [ObservableProperty]
    private CategoryFilterOption? _selectedCategoryFilter = AllCategories;

    [ObservableProperty]
    private Option<TransactionKind?>? _selectedKindFilter = AllKinds;

    [ObservableProperty]
    private DateTime? _fromMonth;

    [ObservableProperty]
    private DateTime? _toMonth;

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

    public override string Title => "Gastos";

    public ObservableCollection<TransactionRowViewModel> Rows { get; } = [];

    public ObservableCollection<CardOption> CardOptions { get; } = [AllCards];

    public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; } = [AllCategories];

    public IReadOnlyList<Option<TransactionKind?>> KindFilterOptions { get; } =
        [AllKinds, .. Options.TransactionKinds.Select(k => new Option<TransactionKind?>(k.Value, k.Label))];

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
        SelectedCard = AllCards;
        SelectedCategoryFilter = AllCategories;
        SelectedKindFilter = AllKinds;
        FromMonth = null;
        ToMonth = null;
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
        RuleSuggestion? suggestion = null;
        var ok = await RunAsync(async () =>
            suggestion = await UseCases.RunAsync<TransactionService, RuleSuggestion?>((s, ct) => s.CategorizeAsync(row.Item.Id, option.Id, ct)));
        StatusMessage = ok ? $"\"{row.Item.MerchantName}\" → {option.Label}" : null;

        if (!ok)
        {
            await RunAsync(SearchPageAsync);
        }
        else if (suggestion is { } rule)
        {
            await OfferRuleAsync(rule);
        }
    }

    /// <summary>Aprendizado: após uma classificação manual, oferece criar a regra para lançamentos semelhantes.</summary>
    private async Task OfferRuleAsync(RuleSuggestion rule)
    {
        var similar = rule.MatchingUncategorized > 0
            ? $"\n\n{rule.MatchingUncategorized} lançamento(s) sem categoria também correspondem e serão categorizados agora."
            : string.Empty;

        var accepted = await Dialogs.ConfirmAsync(
            "Criar regra?",
            $"Deseja aplicar essa regra para futuras transações semelhantes?\n\n\"{rule.Pattern}\" → {rule.CategoryName}{similar}\n\nVocê pode editar ou excluir a regra em Categorias › Regras.",
            "Criar regra");

        if (!accepted)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await UseCases.RunAsync<CategoryRuleService, Guid>((s, ct) => s.CreateAsync(new SaveCategoryRuleCommand(rule.Pattern, rule.CategoryId, 0), ct));
            var applied = await UseCases.RunAsync<CategoryRuleService, int>((s, ct) => s.ApplyToUncategorizedAsync(ct));
            StatusMessage = $"Regra \"{rule.Pattern}\" → {rule.CategoryName} criada." + (applied > 0 ? $" {applied} lançamento(s) categorizado(s)." : string.Empty);
            await SearchPageAsync();
        });
    }

    private async Task ChangeKindAsync(TransactionRowViewModel row, TransactionKind kind)
    {
        var ok = await RunAsync(() => UseCases.RunAsync<TransactionService>((s, ct) => s.ChangeKindAsync(row.Item.Id, kind, ct)));
        StatusMessage = ok ? $"\"{row.Item.MerchantName}\" agora é {Services.Labels.For(kind).ToLowerInvariant()}." : null;

        // Recarrega sempre: o valor de gasto depende do tipo (e, em caso de erro, o ComboBox volta ao tipo gravado).
        await RunAsync(SearchPageAsync);
    }

    private async Task SearchPageAsync()
    {
        var search = new TransactionSearch
        {
            CreditCardId = SelectedCard?.Id,
            FromMonth = FromMonth is { } from ? Months.Of(DateOnly.FromDateTime(from)) : null,
            ToMonth = ToMonth is { } to ? Months.Of(DateOnly.FromDateTime(to)) : null,
            CategoryId = SelectedCategoryFilter?.CategoryId,
            UncategorizedOnly = SelectedCategoryFilter?.UncategorizedOnly ?? false,
            Kind = SelectedKindFilter?.Value,
            Text = SearchText,
            Page = Page,
            PageSize = PageSize,
        };

        var result = await UseCases.RunAsync<TransactionService, PagedResult<TransactionListItem>>((s, ct) => s.SearchAsync(search, ct));

        Rows.Clear();
        foreach (var item in result.Items)
        {
            Rows.Add(new TransactionRowViewModel(item, _categoryOptions, CategorizeAsync, ChangeKindAsync));
        }

        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
    }

    private async Task LoadFilterOptionsAsync()
    {
        var cards = await UseCases.RunAsync<CreditCardService, IReadOnlyList<CreditCardDto>>((s, ct) => s.ListAsync(true, ct));
        var categories = await UseCases.RunAsync<CategoryService, IReadOnlyList<CategoryDto>>((s, ct) => s.ListAsync(true, ct));

        var card = SelectedCard;
        CardOptions.Clear();
        CardOptions.Add(AllCards);
        foreach (var c in cards)
        {
            CardOptions.Add(new CardOption(c.Id, c.Name));
        }

        SelectedCard = CardOptions.FirstOrDefault(o => o == card) ?? AllCards;

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
using CommunityToolkit.Mvvm.ComponentModel;

using MyFinance.Application.Categories;
using MyFinance.Desktop.Services;
using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Desktop.ViewModels;

public sealed record NavigationItem(string Title, PageViewModel Page, bool StartsGroup);

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IUseCaseExecutor _useCases;
    private readonly PageServices _pageServices;

    [ObservableProperty]
    private NavigationItem? _selectedNavigation;

    [ObservableProperty]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private bool _isInitializing = true;

    public MainWindowViewModel(
        DialogService dialogs,
        IUseCaseExecutor useCases,
        PageServices pageServices,
        DashboardViewModel dashboard,
        AccountsViewModel accounts,
        CreditCardsViewModel creditCards,
        TransactionsViewModel transactions,
        CategoriesViewModel categories,
        ImportViewModel import,
        SettingsViewModel settings)
    {
        Dialogs = dialogs;
        _useCases = useCases;
        _pageServices = pageServices;
        Navigation =
        [
            new("Dashboard", dashboard, false),
            new("Contas", accounts, true),
            new("Cartões", creditCards, false),
            new("Transações", transactions, false),
            new("Categorias", categories, false),
            new("Importar Extrato", import, true),
            new("Configurações", settings, true),
        ];
    }

    public string Title => "MyFinance";

    public DialogService Dialogs { get; }

    public IReadOnlyList<NavigationItem> Navigation { get; }

    /// <summary>Aplica migrations, cria as categorias padrão no primeiro uso e abre o dashboard.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _useCases.RunAsync<DatabaseInitializer>((s, ct) => s.InitializeAsync(ct));
            await _useCases.RunAsync<CategoryService>((s, ct) => s.EnsureDefaultCategoriesAsync(ct));
            IsInitializing = false;
            SelectedNavigation = Navigation[0];
        }
        catch (Exception ex)
        {
            await _pageServices.HandleErrorAsync(ex);
        }
    }

    partial void OnSelectedNavigationChanged(NavigationItem? value)
    {
        if (value is null)
        {
            return;
        }

        CurrentPage = value.Page;
        _ = value.Page.LoadAsync();
    }
}
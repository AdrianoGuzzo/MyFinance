using CommunityToolkit.Mvvm.ComponentModel;

using MyFinance.Application.Categories;
using MyFinance.Desktop.Services;
using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Desktop.ViewModels;

public sealed record NavigationItem(string Title, PageViewModel Page, bool StartsGroup)
{
    /// <summary>Também é o nome lido pelos leitores de tela (automação).</summary>
    public override string ToString() => Title;
}

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
        CreditCardsViewModel creditCards,
        TransactionsViewModel transactions,
        InvoicesViewModel invoices,
        InstallmentsViewModel installments,
        ReportsViewModel reports,
        LimitsViewModel limits,
        RecurringViewModel recurring,
        StrategyViewModel strategy,
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
            new("Gastos", transactions, false),
            new("Faturas", invoices, false),
            new("Cartões", creditCards, false),
            new("Parcelamentos", installments, false),
            new("Categorias", categories, true),
            new("Limites", limits, false),
            new("Recorrentes", recurring, false),
            new("Estratégia", strategy, false),
            new("Relatórios", reports, true),
            new("Importação", import, true),
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
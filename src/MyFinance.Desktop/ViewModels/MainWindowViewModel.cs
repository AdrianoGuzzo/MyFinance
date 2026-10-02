using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using MyFinance.Application.Categories;
using MyFinance.Desktop.Services;
using MyFinance.Infrastructure.Persistence;
using MyFinance.Mcp;

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
    private readonly McpServerCoordinator _mcp;
    private readonly HashSet<PageViewModel> _refreshOnExternalChange;
    private readonly DispatcherTimer _externalChangeDebounce = new() { Interval = TimeSpan.FromMilliseconds(700) };

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
        McpServerCoordinator mcp,
        DataChangeNotifier dataChanges,
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
        _mcp = mcp;
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

        // Telas cujos dados o servidor MCP pode alterar (categorias, regras, metas, limites, recorrentes).
        _refreshOnExternalChange = [dashboard, transactions, reports, limits, recurring, strategy, categories];
        _externalChangeDebounce.Tick += (_, _) => RefreshAfterExternalChange();
        dataChanges.DataChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            // Um lote de alterações gera uma única recarga.
            _externalChangeDebounce.Stop();
            _externalChangeDebounce.Start();
        });
    }

    public string Title => "MyFinance";

    public DialogService Dialogs { get; }

    public IReadOnlyList<NavigationItem> Navigation { get; }

    /// <summary>
    /// Aplica migrations, cria as categorias padrão no primeiro uso, abre o dashboard e, se o usuário ligou, inicia o servidor MCP.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _useCases.RunAsync<DatabaseInitializer>((s, ct) => s.InitializeAsync(ct));
            await _useCases.RunAsync<CategoryService>((s, ct) => s.EnsureDefaultCategoriesAsync(ct));
            IsInitializing = false;
            SelectedNavigation = Navigation[0];

            if (await _mcp.StartIfEnabledAsync(CancellationToken.None) is { } error)
            {
                await Dialogs.ShowErrorAsync("Servidor MCP", error);
            }
        }
        catch (Exception ex)
        {
            await _pageServices.HandleErrorAsync(ex);
        }
    }

    /// <summary>Recarrega a tela aberta depois que o servidor MCP alterou dados (se ela não estiver ocupada nem com um diálogo).</summary>
    private void RefreshAfterExternalChange()
    {
        _externalChangeDebounce.Stop();
        if (CurrentPage is { IsBusy: false } page && _refreshOnExternalChange.Contains(page) && Dialogs.Current is null)
        {
            _ = page.LoadAsync();
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
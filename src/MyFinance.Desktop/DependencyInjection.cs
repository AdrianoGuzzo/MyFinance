using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using MyFinance.Desktop.Services;
using MyFinance.Desktop.ViewModels;
using MyFinance.Mcp;

namespace MyFinance.Desktop;

internal static class DependencyInjection
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        services.AddSingleton<IUseCaseExecutor, UseCaseExecutor>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());
        services.AddSingleton<UserSettingsStore>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ClipboardService>();

        // Servidor MCP local (desligado por padrão): mesmo banco, logs e relógio do aplicativo.
        services.AddSingleton<DataChangeNotifier>();
        services.AddSingleton(sp => new McpServerHost(
            new McpServerHostOptions(sp.GetRequiredService<AppPaths>().DatabasePath),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<DataChangeNotifier>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<McpServerCoordinator>();
        services.AddSingleton<PageServices>();

        // Telas vivem durante toda a execução; cada operação abre seu próprio escopo (IUseCaseExecutor).
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<CreditCardsViewModel>();
        services.AddSingleton<TransactionsViewModel>();
        services.AddSingleton<CategoriesViewModel>();
        services.AddSingleton<CategoryRulesViewModel>();
        services.AddSingleton<InvoicesViewModel>();
        services.AddSingleton<InstallmentsViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<LimitsViewModel>();
        services.AddSingleton<RecurringViewModel>();
        services.AddSingleton<StrategyViewModel>();
        services.AddSingleton<ImportViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        return services;
    }
}
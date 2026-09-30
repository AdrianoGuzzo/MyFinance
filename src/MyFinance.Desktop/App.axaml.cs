using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using MyFinance.Desktop.Services;
using MyFinance.Desktop.ViewModels;
using MyFinance.Desktop.Views;

namespace MyFinance.Desktop;

public partial class App : Avalonia.Application
{
    private readonly IServiceProvider? _services;

    public App() { }

    public App(IServiceProvider services) => _services = services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (_services is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services.GetRequiredService<ThemeService>().ApplySaved();

            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = viewModel };
            _services.GetRequiredService<FilePickerService>().Attach(window);

            // Composição apenas: a inicialização (migrations, categorias padrão) é do ViewModel.
            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
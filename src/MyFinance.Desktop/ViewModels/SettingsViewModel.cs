using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Extensions.Hosting;

using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ThemeService _themeService;

    [ObservableProperty]
    private Option<AppTheme> _selectedTheme;

    public SettingsViewModel(PageServices services, ThemeService themeService, AppPaths paths, IHostEnvironment environment)
        : base(services)
    {
        _themeService = themeService;
        _selectedTheme = Themes.First(t => t.Value == themeService.Current);
        DatabasePath = paths.DatabasePath;
        LogDirectory = paths.LogDirectory;
        EnvironmentName = environment.EnvironmentName;
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
    }

    public override string Title => "Configurações";

    public IReadOnlyList<Option<AppTheme>> Themes => Options.Themes;

    public string DatabasePath { get; }

    public string LogDirectory { get; }

    public string EnvironmentName { get; }

    public string Version { get; }

    public override Task LoadAsync()
    {
        SelectedTheme = Themes.First(t => t.Value == _themeService.Current);
        return Task.CompletedTask;
    }

    partial void OnSelectedThemeChanged(Option<AppTheme> value)
    {
        if (value.Value != _themeService.Current)
        {
            _themeService.Apply(value.Value);
            StatusMessage = $"Tema: {value.Label}.";
        }
    }
}
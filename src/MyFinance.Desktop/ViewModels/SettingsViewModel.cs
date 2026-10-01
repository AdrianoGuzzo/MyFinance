using System.Globalization;
using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Hosting;

using MyFinance.Application.Backup;
using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ThemeService _themeService;
    private readonly IFilePickerService _filePicker;

    [ObservableProperty]
    private Option<AppTheme> _selectedTheme;

    public SettingsViewModel(
        PageServices services, ThemeService themeService, IFilePickerService filePicker, AppPaths paths, IHostEnvironment environment)
        : base(services)
    {
        _themeService = themeService;
        _filePicker = filePicker;
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

    /// <summary>Grava uma cópia do banco local em um arquivo escolhido pelo usuário.</summary>
    [RelayCommand]
    private async Task BackupAsync()
    {
        var name = $"myfinance-backup-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.db";
        string? destination = null;
        if (!await RunAsync(async () => destination = await _filePicker.PickBackupDestinationAsync(name)) || destination is null)
        {
            return;
        }

        if (await RunAsync(() => UseCases.RunAsync<IDatabaseBackup>((s, ct) => s.BackupToAsync(destination, ct)), "Gravando cópia de segurança..."))
        {
            StatusMessage = $"Cópia de segurança gravada em {destination}.";
        }
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
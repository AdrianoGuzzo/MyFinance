using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Hosting;

using MyFinance.Application.Backup;
using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly ThemeService _themeService;
    private readonly IFilePickerService _filePicker;
    private readonly McpServerCoordinator _mcp;
    private readonly ClipboardService _clipboard;
    private bool _syncingMcp;

    [ObservableProperty]
    private Option<AppTheme> _selectedTheme;

    [ObservableProperty]
    private bool _isMcpEnabled;

    [ObservableProperty]
    private decimal? _mcpPort;

    [ObservableProperty]
    private string _mcpStatus = string.Empty;

    [ObservableProperty]
    private bool _isMcpRunning;

    [ObservableProperty]
    private string _claudeCodeCommand = string.Empty;

    [ObservableProperty]
    private string _claudeDesktopConfig = string.Empty;

    [ObservableProperty]
    private bool _hasMcpToken;

    public SettingsViewModel(
        PageServices services, ThemeService themeService, IFilePickerService filePicker, AppPaths paths, IHostEnvironment environment,
        McpServerCoordinator mcp, ClipboardService clipboard)
        : base(services)
    {
        _themeService = themeService;
        _filePicker = filePicker;
        _mcp = mcp;
        _clipboard = clipboard;
        _selectedTheme = Themes.First(t => t.Value == themeService.Current);
        DatabasePath = paths.DatabasePath;
        LogDirectory = paths.LogDirectory;
        EnvironmentName = environment.EnvironmentName;
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        IsMcpEnabledLocked = mcp.IsEnabledLockedByConfiguration;
        IsMcpPortLocked = mcp.IsPortLockedByConfiguration;

        mcp.Host.StateChanged += (_, _) => Dispatcher.UIThread.Post(RefreshMcp);
        RefreshMcp();
    }

    public override string Title => "Configurações";

    public IReadOnlyList<Option<AppTheme>> Themes => Options.Themes;

    public string DatabasePath { get; }

    public string LogDirectory { get; }

    public string EnvironmentName { get; }

    public string Version { get; }

    public decimal MinPort => McpServerCoordinator.MinPort;

    public decimal MaxPort => McpServerCoordinator.MaxPort;

    /// <summary><c>Mcp:Enabled</c> definido no appsettings ou na linha de comando.</summary>
    public bool IsMcpEnabledLocked { get; }

    /// <summary><c>Mcp:Port</c> definido no appsettings ou na linha de comando.</summary>
    public bool IsMcpPortLocked { get; }

    public bool CanToggleMcp => !IsMcpEnabledLocked;

    public bool CanChangeMcpPort => !IsMcpPortLocked;

    public override Task LoadAsync()
    {
        SelectedTheme = Themes.First(t => t.Value == _themeService.Current);
        RefreshMcp();
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

    [RelayCommand]
    private async Task ApplyMcpPortAsync()
    {
        var port = (int)(McpPort ?? 0);
        if (await RunAsync(() => _mcp.ChangePortAsync(port, CancellationToken.None), "Aplicando porta..."))
        {
            StatusMessage = IsMcpRunning ? $"Servidor MCP reiniciado na porta {port}." : $"Porta do servidor MCP: {port}.";
        }

        RefreshMcp();
    }

    [RelayCommand]
    private async Task RegenerateMcpTokenAsync()
    {
        if (!await Dialogs.ConfirmAsync(
            "Gerar novo token",
            "Assistentes já configurados deixarão de ter acesso até receberem a nova configuração. Continuar?",
            "Gerar novo token"))
        {
            return;
        }

        if (await RunAsync(() => _mcp.RegenerateTokenAsync(CancellationToken.None)))
        {
            StatusMessage = "Novo token gerado. Copie a configuração novamente nos assistentes.";
        }

        RefreshMcp();
    }

    [RelayCommand]
    private Task CopyClaudeCodeCommandAsync() => CopyAsync(ClaudeCodeCommand, "Comando do Claude Code copiado.");

    [RelayCommand]
    private Task CopyClaudeDesktopConfigAsync() => CopyAsync(ClaudeDesktopConfig, "Configuração do Claude Desktop copiada.");

    private async Task CopyAsync(string text, string message)
    {
        if (await RunAsync(() => _clipboard.CopyAsync(text)))
        {
            StatusMessage = message;
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

    partial void OnIsMcpEnabledChanged(bool value)
    {
        if (!_syncingMcp)
        {
            _ = ToggleMcpAsync(value);
        }
    }

    private async Task ToggleMcpAsync(bool enabled)
    {
        if (await RunAsync(() => _mcp.SetEnabledAsync(enabled, CancellationToken.None), enabled ? "Iniciando servidor MCP..." : "Parando servidor MCP..."))
        {
            StatusMessage = enabled ? "Servidor MCP ligado." : "Servidor MCP desligado.";
        }

        RefreshMcp();
    }

    /// <summary>Atualiza a tela com o estado real do servidor e das preferências (também desfaz o botão se ligar falhou).</summary>
    private void RefreshMcp()
    {
        _syncingMcp = true;
        try
        {
            IsMcpEnabled = _mcp.IsEnabled;
            McpPort = _mcp.Port;
            IsMcpRunning = _mcp.Host.IsRunning;
            McpStatus = (_mcp.Host.Endpoint, _mcp.IsEnabled) switch
            {
                ({ } endpoint, _) => $"Ligado em {endpoint}",
                (null, true) => "Ligado, mas não foi possível iniciar. Verifique a porta e tente novamente.",
                _ => "Desligado",
            };

            var token = _mcp.Token;
            HasMcpToken = token is not null;
            var url = $"http://127.0.0.1:{_mcp.Port}/mcp";
            ClaudeCodeCommand = token is null
                ? "Ligue o servidor para gerar o token de acesso."
                : $"claude mcp add --transport http myfinance {url} --header \"Authorization: Bearer {token}\"";
            ClaudeDesktopConfig = token is null ? string.Empty : DesktopConfig(url, token);
        }
        finally
        {
            _syncingMcp = false;
        }
    }

    /// <summary>
    /// Trecho para o claude_desktop_config.json. O Claude Desktop conecta servidores locais via processo,
    /// então usa o <c>mcp-remote</c> como ponte HTTP; o cabeçalho vai por variável de ambiente para evitar problemas com espaços.
    /// </summary>
    private static string DesktopConfig(string url, string token)
    {
        var config = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["myfinance"] = new JsonObject
                {
                    ["command"] = "npx",
                    ["args"] = new JsonArray("-y", "mcp-remote", url, "--header", "Authorization:${MYFINANCE_AUTH}"),
                    ["env"] = new JsonObject { ["MYFINANCE_AUTH"] = $"Bearer {token}" },
                },
            },
        };
        return config.ToJsonString(IndentedJson);
    }
}
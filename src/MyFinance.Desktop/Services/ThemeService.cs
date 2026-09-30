using System.Text.Json;

using Avalonia.Styling;

using Microsoft.Extensions.Logging;

namespace MyFinance.Desktop.Services;

public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

public sealed record UserSettings(AppTheme Theme = AppTheme.System);

/// <summary>Preferências do usuário (tema), gravadas em JSON na pasta de dados local.</summary>
public sealed partial class ThemeService(AppPaths paths, ILogger<ThemeService> logger)
{
    public AppTheme Current { get; private set; }

    public void ApplySaved()
    {
        var settings = Load();
        Apply(settings.Theme, persist: false);
    }

    public void Apply(AppTheme theme, bool persist = true)
    {
        Current = theme;
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        if (persist)
        {
            Save(new UserSettings(theme));
        }
    }

    private UserSettings Load()
    {
        try
        {
            return File.Exists(paths.UserSettingsFile)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(paths.UserSettingsFile)) ?? new UserSettings()
                : new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            LogSettingsError(logger, ex);
            return new UserSettings();
        }
    }

    private void Save(UserSettings settings)
    {
        try
        {
            File.WriteAllText(paths.UserSettingsFile, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSettingsError(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível ler/gravar as preferências do usuário")]
    private static partial void LogSettingsError(ILogger logger, Exception exception);
}
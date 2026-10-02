using Avalonia.Styling;

namespace MyFinance.Desktop.Services;

public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Tema da interface, gravado nas preferências do usuário.</summary>
public sealed class ThemeService(UserSettingsStore settings)
{
    public AppTheme Current { get; private set; }

    public void ApplySaved() => Apply(settings.Load().Theme, persist: false);

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
            settings.Update(s => s with { Theme = theme });
        }
    }
}
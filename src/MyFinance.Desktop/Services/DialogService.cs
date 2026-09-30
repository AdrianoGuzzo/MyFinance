using CommunityToolkit.Mvvm.ComponentModel;

using MyFinance.Desktop.ViewModels;

namespace MyFinance.Desktop.Services;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    Task ShowErrorAsync(string title, string message);

    /// <returns><c>true</c> se o usuário confirmar.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Confirmar", bool isDestructive = false);
}

/// <summary>
/// Diálogos exibidos como sobreposição na janela principal (sem janelas extras).
/// Um diálogo por vez; pedidos simultâneos aguardam na fila.
/// </summary>
public sealed partial class DialogService : ObservableObject, IDialogService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    [ObservableProperty]
    private DialogViewModel? _current;

    public Task ShowMessageAsync(string title, string message) =>
        ShowAsync(new DialogViewModel(title, message, "OK", cancelText: null, isError: false, isDestructive: false));

    public Task ShowErrorAsync(string title, string message) =>
        ShowAsync(new DialogViewModel(title, message, "OK", cancelText: null, isError: true, isDestructive: false));

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Confirmar", bool isDestructive = false) =>
        ShowAsync(new DialogViewModel(title, message, confirmText, "Cancelar", isError: false, isDestructive));

    public void Dispose() => _gate.Dispose();

    private async Task<bool> ShowAsync(DialogViewModel dialog)
    {
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            Current = dialog;
            return await dialog.Result.ConfigureAwait(true);
        }
        finally
        {
            Current = null;
            _gate.Release();
        }
    }
}
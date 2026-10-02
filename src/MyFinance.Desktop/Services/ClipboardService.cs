using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace MyFinance.Desktop.Services;

/// <summary>Área de transferência da janela principal.</summary>
public sealed class ClipboardService
{
    private TopLevel? _topLevel;

    public void Attach(TopLevel topLevel) => _topLevel = topLevel;

    public async Task CopyAsync(string text)
    {
        var clipboard = _topLevel?.Clipboard ?? throw new InvalidOperationException("Janela principal ainda não disponível.");
        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }
}
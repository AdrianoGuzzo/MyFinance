using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MyFinance.Desktop.Services;

public sealed record PickedFile(string Name, Func<Task<Stream>> OpenReadAsync);

public interface IFilePickerService
{
    /// <returns><c>null</c> se o usuário cancelar.</returns>
    Task<PickedFile?> PickStatementAsync();

    /// <summary>Local para salvar a cópia de segurança do banco.</summary>
    /// <returns>Caminho local escolhido; <c>null</c> se o usuário cancelar.</returns>
    Task<string?> PickBackupDestinationAsync(string suggestedName);
}

internal sealed class FilePickerService : IFilePickerService
{
    private static readonly FilePickerFileType Statements = new("Extratos (OFX, CSV)") { Patterns = ["*.ofx", "*.qfx", "*.csv"] };
    private static readonly FilePickerFileType Databases = new("Banco de dados (*.db)") { Patterns = ["*.db"] };
    private static readonly FilePickerFileType AllFiles = new("Todos os arquivos") { Patterns = ["*"] };

    private TopLevel? _topLevel;

    public void Attach(TopLevel topLevel) => _topLevel = topLevel;

    public async Task<PickedFile?> PickStatementAsync()
    {
        var storage = _topLevel?.StorageProvider ?? throw new InvalidOperationException("Janela principal ainda não disponível.");

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar extrato",
            AllowMultiple = false,
            FileTypeFilter = [Statements, AllFiles],
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : new PickedFile(files[0].Name, files[0].OpenReadAsync);
    }

    public async Task<string?> PickBackupDestinationAsync(string suggestedName)
    {
        var storage = _topLevel?.StorageProvider ?? throw new InvalidOperationException("Janela principal ainda não disponível.");

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar cópia de segurança",
            SuggestedFileName = suggestedName,
            DefaultExtension = "db",
            ShowOverwritePrompt = true,
            FileTypeChoices = [Databases],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }
}
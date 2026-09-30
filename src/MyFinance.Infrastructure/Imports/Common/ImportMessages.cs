namespace MyFinance.Infrastructure.Imports.Common;

internal static class ImportMessages
{
    private const int MaxQuotedLength = 40;

    /// <summary>Trecho do valor original para a mensagem de erro (campos quebrados podem ter milhares de caracteres).</summary>
    public static string Quote(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= MaxQuotedLength ? $"\"{text}\"" : $"\"{text[..MaxQuotedLength]}…\"";
    }
}

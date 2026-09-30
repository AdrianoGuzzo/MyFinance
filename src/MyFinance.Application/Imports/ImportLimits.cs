namespace MyFinance.Application.Imports;

public static class ImportLimits
{
    /// <summary>Extratos pessoais têm poucos KB/MB; o limite evita carregar arquivos errados enormes.</summary>
    public const int MaxFileSizeBytes = 20 * 1024 * 1024;
}
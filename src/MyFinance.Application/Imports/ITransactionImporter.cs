namespace MyFinance.Application.Imports;

/// <summary>
/// Converte um arquivo de extrato em <see cref="ImportedTransaction"/>.
/// Implementações não conhecem contas, duplicidades nem banco de dados.
/// </summary>
public interface ITransactionImporter
{
    bool CanHandle(string fileName);

    /// <exception cref="Common.Exceptions.ImportException">
    /// Quando o arquivo inteiro é ilegível (vazio, formato inválido, colunas não identificadas).
    /// Registros individuais inválidos não geram exceção: vão para <see cref="ImportResult.Errors"/>.
    /// </exception>
    Task<ImportResult> ImportAsync(Stream stream, CancellationToken cancellationToken);
}
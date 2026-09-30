using MyFinance.Application.Common.Exceptions;

namespace MyFinance.Application.Imports;

/// <summary>Etapa "Identificar formato": escolhe o importador adequado ao arquivo.</summary>
public sealed class TransactionImporterResolver(IEnumerable<ITransactionImporter> importers)
{
    private readonly IReadOnlyList<ITransactionImporter> _importers = [.. importers];

    public ITransactionImporter Resolve(string fileName) =>
        _importers.FirstOrDefault(i => i.CanHandle(fileName))
        ?? throw new ImportException("O formato do arquivo não é reconhecido. Use arquivos OFX ou CSV.");
}
using Microsoft.Extensions.Logging;

namespace MyFinance.Mcp.Logging;

/// <summary>Encaminha os logs do servidor MCP para a fábrica de logs do aplicativo (mesmos arquivos e filtros).</summary>
internal sealed class ForwardingLoggerProvider(ILoggerFactory target) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => target.CreateLogger(categoryName);

    // A fábrica pertence ao aplicativo; não é descartada aqui.
    public void Dispose() { }
}

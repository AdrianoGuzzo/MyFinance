using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Logging;

namespace MyFinance.Mcp.Logging;

/// <summary>Logs do servidor MCP: só metadados técnicos, nunca argumentos ou resultados das ferramentas.</summary>
internal static partial class McpLog
{
    [LoggerMessage(EventId = LogEvents.McpServerStarted, EventName = nameof(LogEvents.McpServerStarted), Level = LogLevel.Information,
        Message = "Servidor MCP iniciado na porta {Port}")]
    public static partial void ServerStarted(ILogger logger, int port);

    [LoggerMessage(EventId = LogEvents.McpServerStopped, EventName = nameof(LogEvents.McpServerStopped), Level = LogLevel.Information,
        Message = "Servidor MCP parado")]
    public static partial void ServerStopped(ILogger logger);

    [LoggerMessage(EventId = LogEvents.McpServerStartFailed, EventName = nameof(LogEvents.McpServerStartFailed), Level = LogLevel.Warning,
        Message = "Não foi possível iniciar o servidor MCP na porta {Port}")]
    public static partial void ServerStartFailed(ILogger logger, int port, Exception exception);

    [LoggerMessage(EventId = LogEvents.McpRequestRejected, EventName = nameof(LogEvents.McpRequestRejected), Level = LogLevel.Warning,
        Message = "Requisição ao servidor MCP recusada ({Reason})")]
    public static partial void RequestRejected(ILogger logger, string reason);

    [LoggerMessage(EventId = LogEvents.McpToolFailed, EventName = nameof(LogEvents.McpToolFailed), Level = LogLevel.Error,
        Message = "Erro inesperado na ferramenta MCP {Tool}")]
    public static partial void ToolFailed(ILogger logger, string tool, Exception exception);
}

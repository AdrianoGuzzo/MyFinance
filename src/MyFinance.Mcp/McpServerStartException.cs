using MyFinance.Application.Common.Exceptions;

namespace MyFinance.Mcp;

/// <summary>O servidor MCP não pôde ser iniciado (porta em uso ou reservada).</summary>
public sealed class McpServerStartException : AppException
{
    public McpServerStartException(string message) : base(message) { }

    public McpServerStartException(string message, Exception? innerException) : base(message, innerException) { }
}

namespace MyFinance.Application.Common.Logging;

/// <summary>Identificadores dos eventos técnicos registrados em log.</summary>
public static class LogEvents
{
    public const int ApplicationStarted = 1000;
    public const int DatabaseMigrated = 1100;
    public const int DatabaseError = 1101;

    /// <summary>Banco de uma versão anterior (esquema desconhecido) movido para a pasta de backups e recriado.</summary>
    public const int DatabaseReset = 1102;

    public const int DatabaseBackup = 1103;
    public const int ImportStarted = 2000;
    public const int ImportCompleted = 2001;
    public const int ImportFailed = 2002;

    /// <summary>Arquivo recusado por ser inválido (formato, vazio...) — erro esperado, não técnico.</summary>
    public const int ImportRejected = 2003;

    public const int McpServerStarted = 3000;
    public const int McpServerStopped = 3001;
    public const int McpServerStartFailed = 3002;

    /// <summary>Requisição recusada pelo servidor MCP (host, origem ou token inválidos) — registra só o motivo.</summary>
    public const int McpRequestRejected = 3003;

    /// <summary>Erro inesperado em uma ferramenta MCP — registra só o nome da ferramenta.</summary>
    public const int McpToolFailed = 3004;

    public const int UnhandledException = 9000;

    /// <summary>Erro inesperado tratado pela interface (a aplicação continua funcionando).</summary>
    public const int TechnicalError = 9001;
}
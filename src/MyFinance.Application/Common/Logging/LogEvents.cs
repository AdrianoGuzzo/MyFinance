namespace MyFinance.Application.Common.Logging;

/// <summary>Identificadores dos eventos técnicos registrados em log.</summary>
public static class LogEvents
{
    public const int ApplicationStarted = 1000;
    public const int DatabaseMigrated = 1100;
    public const int DatabaseError = 1101;
    public const int ImportStarted = 2000;
    public const int ImportCompleted = 2001;
    public const int ImportFailed = 2002;

    /// <summary>Arquivo recusado por ser inválido (formato, vazio...) — erro esperado, não técnico.</summary>
    public const int ImportRejected = 2003;

    public const int UnhandledException = 9000;

    /// <summary>Erro inesperado tratado pela interface (a aplicação continua funcionando).</summary>
    public const int TechnicalError = 9001;
}
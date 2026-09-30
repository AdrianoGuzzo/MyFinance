using Microsoft.Extensions.Configuration;

using Serilog;

namespace MyFinance.Infrastructure.Logging;

/// <summary>
/// Logs técnicos estruturados em arquivo local, com rolagem diária (<c>logs/myfinance-AAAAMMDD.log</c>).
/// Nível mínimo e overrides vêm da seção <c>Serilog</c> do appsettings.
/// Regra do projeto: nunca registrar número de conta/cartão completo, descrições, valores ou conteúdo de extratos.
/// </summary>
public static class SerilogSetup
{
    public const string FileNamePattern = "myfinance-.log";

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} ({EventId}) {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static LoggerConfiguration Configure(LoggerConfiguration logger, IConfiguration configuration, string logDirectory)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return logger
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, FileNamePattern),
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture);
    }
}
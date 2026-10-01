using System.Globalization;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Logging;

namespace MyFinance.Infrastructure.Persistence;

/// <summary>
/// Prepara o banco na inicialização. Nunca usa <c>EnsureCreated()</c>:
/// <list type="bullet">
/// <item><description>banco com migrations que esta versão não conhece (esquema anterior ao refoco em cartões) é movido
/// para a pasta <c>backups</c> e recriado — nada é apagado;</description></item>
/// <item><description>antes de aplicar migrations pendentes em um banco com dados, grava uma cópia em <c>backups</c> (<c>VACUUM INTO</c>);</description></item>
/// <item><description>aplica as migrations pendentes (<c>MigrateAsync</c>).</description></item>
/// </list>
/// </summary>
public sealed partial class DatabaseInitializer(
    FinanceDbContext db,
    ILogger<DatabaseInitializer> logger,
    TimeProvider? timeProvider = null)
{
    public const string BackupFolderName = "backups";

    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var path = DatabaseFilePath();
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        if (applied.Any(m => !known.Contains(m)))
        {
            if (path is null)
            {
                throw new InvalidOperationException("O banco em memória contém migrations desconhecidas.");
            }

            await db.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();
            var legacy = BackupPath(path, "legado");
            File.Move(path, legacy);
            var legacyName = Path.GetFileName(legacy);
            LogReset(logger, legacyName);
            applied.Clear();
        }

        var pending = db.Database.GetMigrations().Except(applied, StringComparer.Ordinal).ToList();
        if (pending.Count > 0 && applied.Count > 0 && path is not null)
        {
            var backup = BackupPath(path, "pre-migracao");
            await SqliteDatabaseBackup.VacuumIntoAsync(db, backup, cancellationToken);
            var backupName = Path.GetFileName(backup);
            LogBackup(logger, backupName);
        }

        await db.Database.MigrateAsync(cancellationToken);

        if (pending.Count > 0)
        {
            LogMigrated(logger, pending.Count, pending);
        }
    }

    /// <summary>Caminho do arquivo do banco, ou <c>null</c> para banco em memória.</summary>
    private string? DatabaseFilePath()
    {
        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        return string.IsNullOrWhiteSpace(dataSource) || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            ? null
            : Path.GetFullPath(dataSource);
    }

    private string BackupPath(string databasePath, string suffix)
    {
        var folder = Path.Combine(Path.GetDirectoryName(databasePath)!, BackupFolderName);
        Directory.CreateDirectory(folder);
        var stamp = _clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = $"{Path.GetFileNameWithoutExtension(databasePath)}-{suffix}-{stamp}{Path.GetExtension(databasePath)}";
        var path = Path.Combine(folder, name);

        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)}-{i}{Path.GetExtension(name)}");
        }

        return path;
    }

    [LoggerMessage(EventId = LogEvents.DatabaseMigrated, EventName = nameof(LogEvents.DatabaseMigrated), Level = LogLevel.Information,
        Message = "Banco de dados atualizado: {Count} migration(s) aplicada(s): {Migrations}")]
    private static partial void LogMigrated(ILogger logger, int count, IReadOnlyList<string> migrations);

    [LoggerMessage(EventId = LogEvents.DatabaseReset, EventName = nameof(LogEvents.DatabaseReset), Level = LogLevel.Warning,
        Message = "Banco de dados de uma versão anterior movido para {BackupFile} e recriado")]
    private static partial void LogReset(ILogger logger, string backupFile);

    [LoggerMessage(EventId = LogEvents.DatabaseBackup, EventName = nameof(LogEvents.DatabaseBackup), Level = LogLevel.Information,
        Message = "Cópia de segurança do banco gravada em {BackupFile}")]
    private static partial void LogBackup(ILogger logger, string backupFile);
}
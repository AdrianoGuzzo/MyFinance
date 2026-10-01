using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyFinance.Application.Backup;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Common.Logging;

namespace MyFinance.Infrastructure.Persistence;

/// <summary>Backup com <c>VACUUM INTO</c>: cópia consistente e compacta, feita com o banco em uso.</summary>
internal sealed partial class SqliteDatabaseBackup(FinanceDbContext db, ILogger<SqliteDatabaseBackup> logger) : IDatabaseBackup
{
    public async Task BackupToAsync(string destinationPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var path = Path.GetFullPath(destinationPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path))
            {
                File.Delete(path); // VACUUM INTO não sobrescreve; o seletor de arquivo já confirmou a substituição.
            }

            await VacuumIntoAsync(db, path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            LogBackupFailed(logger, ex);
            throw new PersistenceException("Não foi possível gravar a cópia de segurança no local escolhido.", ex);
        }

        var name = Path.GetFileName(path);
        LogBackup(logger, name);
    }

    internal static Task VacuumIntoAsync(FinanceDbContext db, string path, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync("VACUUM INTO $path", [new SqliteParameter("$path", path)], cancellationToken);

    // Somente o nome do arquivo: a pasta pode conter o nome do usuário.
    [LoggerMessage(EventId = LogEvents.DatabaseBackup, EventName = nameof(LogEvents.DatabaseBackup), Level = LogLevel.Information,
        Message = "Cópia de segurança do banco gravada em {BackupFile}")]
    private static partial void LogBackup(ILogger logger, string backupFile);

    [LoggerMessage(EventId = LogEvents.DatabaseError, EventName = nameof(LogEvents.DatabaseError), Level = LogLevel.Error,
        Message = "Falha ao gravar a cópia de segurança do banco")]
    private static partial void LogBackupFailed(ILogger logger, Exception exception);
}
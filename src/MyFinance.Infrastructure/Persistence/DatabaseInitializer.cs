using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Logging;

namespace MyFinance.Infrastructure.Persistence;

/// <summary>Aplica as migrations pendentes na inicialização. Nunca usa <c>EnsureCreated()</c>.</summary>
public sealed partial class DatabaseInitializer(FinanceDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        await db.Database.MigrateAsync(cancellationToken);

        if (pending.Count > 0)
        {
            LogMigrated(logger, pending.Count, pending);
        }
    }

    [LoggerMessage(EventId = LogEvents.DatabaseMigrated, EventName = nameof(LogEvents.DatabaseMigrated), Level = LogLevel.Information,
        Message = "Banco de dados atualizado: {Count} migration(s) aplicada(s): {Migrations}")]
    private static partial void LogMigrated(ILogger logger, int count, IReadOnlyList<string> migrations);
}
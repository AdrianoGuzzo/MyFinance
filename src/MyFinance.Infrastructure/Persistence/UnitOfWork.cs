using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Common.Logging;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Infrastructure.Persistence;

internal sealed partial class UnitOfWork(FinanceDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            LogDatabaseError(logger, ex);
            throw new PersistenceException("Não foi possível salvar os dados no banco local.", ex);
        }
    }

    [LoggerMessage(EventId = LogEvents.DatabaseError, EventName = nameof(LogEvents.DatabaseError), Level = LogLevel.Error,
        Message = "Falha ao gravar alterações no banco de dados")]
    private static partial void LogDatabaseError(ILogger logger, Exception exception);
}
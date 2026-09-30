using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Infrastructure.Tests.Persistence;

/// <summary>
/// Banco SQLite real em memória, criado pelas migrations (não por EnsureCreated).
/// A conexão fica aberta durante o teste; cada <see cref="CreateContext"/> simula uma nova unidade de trabalho.
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection) => _connection = connection;

    public static async Task<SqliteTestDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        var database = new SqliteTestDatabase(connection);

        await using var db = database.CreateContext();
        await new DatabaseInitializer(db, NullLogger<DatabaseInitializer>.Instance).InitializeAsync(cancellationToken);
        return database;
    }

    public FinanceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(_connection).Options);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
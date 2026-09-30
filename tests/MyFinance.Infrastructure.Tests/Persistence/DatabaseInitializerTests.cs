using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Infrastructure.Tests.Persistence;

/// <summary>Banco em arquivo temporário, para testar o que acontece com bancos de versões anteriores.</summary>
public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "myfinance-tests", Guid.NewGuid().ToString("N"));

    public DatabaseInitializerTests() => Directory.CreateDirectory(_folder);

    private string DatabasePath => Path.Combine(_folder, "myfinance.db");

    private string BackupFolder => Path.Combine(_folder, DatabaseInitializer.BackupFolderName);

    private FinanceDbContext CreateContext() => new(new DbContextOptionsBuilder<FinanceDbContext>()
        .UseSqlite(SqliteConnectionFactory.BuildConnectionString(DatabasePath))
        .Options);

    private async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await new DatabaseInitializer(db, NullLogger<DatabaseInitializer>.Instance).InitializeAsync(_ct);
    }

    private async Task<IReadOnlyList<string>> TablesAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync(_ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(_ct);
        while (await reader.ReadAsync(_ct))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    [Fact]
    public async Task Banco_de_versao_anterior_e_movido_para_backups_e_recriado()
    {
        // Esquema do MVP de contas bancárias: migration que esta versão não conhece.
        await using (var legacy = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await legacy.OpenAsync(_ct);
            await using var command = legacy.CreateCommand();
            command.CommandText = """
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" TEXT NOT NULL PRIMARY KEY, "ProductVersion" TEXT NOT NULL);
                INSERT INTO "__EFMigrationsHistory" VALUES ('20260930211129_InitialCreate', '10.0.12');
                CREATE TABLE "Accounts" ("Id" TEXT NOT NULL PRIMARY KEY, "Name" TEXT NOT NULL);
                INSERT INTO "Accounts" VALUES ('conta', 'Nubank');
                """;
            await command.ExecuteNonQueryAsync(_ct);
        }

        SqliteConnection.ClearAllPools();

        await InitializeAsync();

        var backup = Directory.GetFiles(BackupFolder, "myfinance-legado-*.db").Should().ContainSingle().Subject;
        (await TablesAsync(backup)).Should().Contain("Accounts", "o banco antigo é preservado, não apagado");
        var tables = await TablesAsync(DatabasePath);
        tables.Should().Contain(["Invoices", "Transactions"]);
        tables.Should().NotContain("Accounts");
    }

    [Fact]
    public async Task Banco_novo_ou_atualizado_nao_gera_backup()
    {
        await InitializeAsync();
        await InitializeAsync();

        Directory.Exists(BackupFolder).Should().BeFalse();
        (await TablesAsync(DatabasePath)).Should().Contain("Invoices");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }
}
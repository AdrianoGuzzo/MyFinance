using Microsoft.Data.Sqlite;

namespace MyFinance.Infrastructure.Persistence;

public static class SqliteConnectionFactory
{
    /// <summary>Local padrão: pasta de dados locais do usuário (ex.: %LOCALAPPDATA%\MyFinance\myfinance.db).</summary>
    public static string DefaultDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyFinance",
        "myfinance.db");

    public static string BuildConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true,
    }.ToString();
}
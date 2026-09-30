using Microsoft.Extensions.Configuration;

using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Desktop.Services;

/// <summary>
/// Locais dos dados do usuário. Caminhos relativos no appsettings são resolvidos a partir de
/// <see cref="DataDirectory"/> (pasta de dados locais do usuário), nunca da pasta do executável.
/// </summary>
public sealed class AppPaths
{
    private AppPaths(string dataDirectory, string databasePath, string logDirectory)
    {
        DataDirectory = dataDirectory;
        DatabasePath = databasePath;
        LogDirectory = logDirectory;
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string LogDirectory { get; }

    public string UserSettingsFile => Path.Combine(DataDirectory, "usersettings.json");

    public static AppPaths From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var dataDirectory = Path.GetDirectoryName(SqliteConnectionFactory.DefaultDatabasePath())!;

        string Resolve(string? configured, string fallback) =>
            string.IsNullOrWhiteSpace(configured) ? fallback : Path.GetFullPath(configured, dataDirectory);

        var paths = new AppPaths(
            dataDirectory,
            Resolve(configuration["Database:Path"], SqliteConnectionFactory.DefaultDatabasePath()),
            Resolve(configuration["Logs:Directory"], Path.Combine(dataDirectory, "logs")));

        Directory.CreateDirectory(paths.DataDirectory);
        Directory.CreateDirectory(paths.LogDirectory);
        return paths;
    }
}
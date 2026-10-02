using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using MyFinance.Mcp;

namespace MyFinance.Desktop.Services;

/// <param name="Enabled">Servidor ligado pelo usuário (desligado por padrão: ligar é o consentimento — ADR 0016).</param>
/// <param name="Token">Token exigido pelo servidor; gerado na primeira vez em que o servidor é ligado.</param>
public sealed record McpUserSettings(bool Enabled = false, int Port = McpServerHost.DefaultPort, string? Token = null);

public sealed record UserSettings(AppTheme Theme = AppTheme.System, McpUserSettings? Mcp = null)
{
    [JsonIgnore]
    public McpUserSettings McpOrDefault => Mcp ?? new McpUserSettings();
}

/// <summary>
/// Preferências do usuário em JSON na pasta de dados local. Cada alteração relê o arquivo e grava o registro inteiro,
/// para que uma preferência (ex.: tema) não apague as outras (ex.: servidor MCP).
/// </summary>
public sealed partial class UserSettingsStore(AppPaths paths, ILogger<UserSettingsStore> logger)
{
    private readonly Lock _lock = new();

    public UserSettings Load()
    {
        lock (_lock)
        {
            return Read();
        }
    }

    public UserSettings Update(Func<UserSettings, UserSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_lock)
        {
            var updated = change(Read());
            try
            {
                File.WriteAllText(paths.UserSettingsFile, JsonSerializer.Serialize(updated));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogSettingsError(logger, ex);
            }

            return updated;
        }
    }

    private UserSettings Read()
    {
        try
        {
            return File.Exists(paths.UserSettingsFile)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(paths.UserSettingsFile)) ?? new UserSettings()
                : new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            LogSettingsError(logger, ex);
            return new UserSettings();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível ler/gravar as preferências do usuário")]
    private static partial void LogSettingsError(ILogger logger, Exception exception);
}
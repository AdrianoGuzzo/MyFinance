using System.Globalization;
using System.Security.Cryptography;

using Microsoft.Extensions.Configuration;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Mcp;

namespace MyFinance.Desktop.Services;

/// <summary>
/// Liga, desliga e reconfigura o servidor MCP conforme as preferências do usuário.
/// <c>Mcp:Enabled</c> e <c>Mcp:Port</c> no appsettings (ou <c>--Mcp:Port=</c>) têm precedência e travam a opção na tela.
/// </summary>
public sealed class McpServerCoordinator(McpServerHost host, UserSettingsStore settings, IConfiguration configuration)
{
    public const int MinPort = 1024;
    public const int MaxPort = 65535;

    public McpServerHost Host { get; } = host;

    public bool IsEnabledLockedByConfiguration => ConfiguredEnabled is not null;

    public bool IsPortLockedByConfiguration => ConfiguredPort is not null;

    public bool IsEnabled => ConfiguredEnabled ?? settings.Load().McpOrDefault.Enabled;

    public int Port => ConfiguredPort ?? settings.Load().McpOrDefault.Port;

    public string? Token => settings.Load().McpOrDefault.Token;

    private bool? ConfiguredEnabled => bool.TryParse(configuration["Mcp:Enabled"], out var enabled) ? enabled : null;

    private int? ConfiguredPort =>
        int.TryParse(configuration["Mcp:Port"], NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= MinPort and <= MaxPort
            ? port
            : null;

    /// <summary>Inicia o servidor se estiver ligado. Chamado depois das migrations, na abertura do aplicativo.</summary>
    /// <returns>Mensagem amigável se não foi possível iniciar; <c>null</c> caso contrário.</returns>
    public async Task<string?> StartIfEnabledAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        try
        {
            await StartAsync(cancellationToken);
            return null;
        }
        catch (McpServerStartException ex)
        {
            return ex.Message;
        }
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        settings.Update(s => s with { Mcp = s.McpOrDefault with { Enabled = enabled } });

        if (enabled)
        {
            try
            {
                await StartAsync(cancellationToken);
            }
            catch (McpServerStartException)
            {
                // Não fica "ligado" sem rodar: o usuário escolhe outra porta e liga de novo.
                settings.Update(s => s with { Mcp = s.McpOrDefault with { Enabled = false } });
                throw;
            }
        }
        else
        {
            await Host.StopAsync(cancellationToken);
        }
    }

    /// <summary>Grava a nova porta e reinicia o servidor se estiver rodando.</summary>
    public async Task ChangePortAsync(int port, CancellationToken cancellationToken)
    {
        if (port is < MinPort or > MaxPort)
        {
            throw new ValidationException($"Escolha uma porta entre {MinPort} e {MaxPort}.");
        }

        settings.Update(s => s with { Mcp = s.McpOrDefault with { Port = port } });
        if (Host.IsRunning)
        {
            await StartAsync(cancellationToken);
        }
    }

    /// <summary>Troca o token (clientes já configurados precisarão do novo) e reinicia o servidor se estiver rodando.</summary>
    public async Task RegenerateTokenAsync(CancellationToken cancellationToken)
    {
        settings.Update(s => s with { Mcp = s.McpOrDefault with { Token = NewToken() } });
        if (Host.IsRunning)
        {
            await StartAsync(cancellationToken);
        }
    }

    private Task StartAsync(CancellationToken cancellationToken) => Host.StartAsync(Port, EnsureToken(), cancellationToken);

    private string EnsureToken() =>
        settings.Load().McpOrDefault.Token
        ?? settings.Update(s => s with { Mcp = s.McpOrDefault with { Token = s.McpOrDefault.Token ?? NewToken() } }).McpOrDefault.Token!;

    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
}
using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MyFinance.Application;
using MyFinance.Infrastructure;
using MyFinance.Mcp.Logging;
using MyFinance.Mcp.Security;

namespace MyFinance.Mcp;

/// <param name="DatabasePath">Mesmo banco SQLite do aplicativo.</param>
public sealed record McpServerHostOptions(string DatabasePath);

/// <summary>
/// Servidor MCP (Streamable HTTP, sem sessão) que roda dentro do aplicativo, ouvindo somente em 127.0.0.1.
/// Tem seu próprio contêiner de serviços (um DbContext por requisição) e compartilha com o aplicativo
/// os logs, o relógio e o aviso de dados alterados.
/// </summary>
public sealed class McpServerHost(
    McpServerHostOptions options, ILoggerFactory loggerFactory, DataChangeNotifier notifier, TimeProvider timeProvider)
    : IAsyncDisposable, IDisposable
{
    public const string Path = "/mcp";
    public const int DefaultPort = 47821;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger = loggerFactory.CreateLogger<McpServerHost>();
    private WebApplication? _app;

    public event EventHandler? StateChanged;

    public bool IsRunning => _app is not null;

    /// <summary>Endereço completo do servidor (ex.: http://127.0.0.1:47821/mcp); <c>null</c> se parado.</summary>
    public Uri? Endpoint { get; private set; }

    /// <summary>Inicia (ou reinicia) o servidor.</summary>
    /// <param name="port">Porta local; 0 escolhe uma porta livre (testes).</param>
    /// <param name="token">Token exigido no cabeçalho <c>Authorization: Bearer</c>.</param>
    /// <exception cref="McpServerStartException">Porta em uso ou reservada.</exception>
    public async Task StartAsync(int port, string token, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync();

            var app = Build(port, token);
            try
            {
                await app.StartAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                McpLog.ServerStartFailed(_logger, port, ex);
                await app.DisposeAsync();
                throw new McpServerStartException(
                    $"A porta {port} já está em uso ou é reservada pelo sistema. Escolha outra porta em Configurações.", ex);
            }

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
            var actualPort = address is null ? port : new Uri(address).Port;
            _app = app;
            Endpoint = new Uri($"http://127.0.0.1:{actualPort}{Path}");
            McpLog.ServerStarted(_logger, actualPort);
        }
        finally
        {
            _lock.Release();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        bool stopped;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            stopped = await StopCoreAsync();
        }
        finally
        {
            _lock.Release();
        }

        if (stopped)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _lock.Dispose();
    }

    /// <summary>O contêiner do aplicativo é descartado de forma síncrona ao fechar; o servidor já deve ter sido parado antes.</summary>
    public void Dispose()
    {
        if (IsRunning)
        {
            Task.Run(() => StopAsync(CancellationToken.None)).Wait(StopTimeout);
        }

        _lock.Dispose();
    }

    private WebApplication Build(int port, string token)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = "MyFinance.Mcp",
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
            Args = [],
        });

        // Nada de fora (variáveis ASPNETCORE_*, appsettings) altera o endereço ou o comportamento do servidor.
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(IPAddress.Loopback, port);
        });

        // Os logs vão para os mesmos destinos do aplicativo. O SDK do MCP registra o JSON das mensagens em Debug/Trace,
        // então os filtros abaixo fazem parte da garantia de privacidade (ADR 0016), independentemente da configuração do Serilog.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ForwardingLoggerProvider(loggerFactory));
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Warning);

        builder.Services.AddSingleton<IHostLifetime, EmbeddedLifetime>();
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(notifier);
        builder.Services
            .AddInfrastructure(options.DatabasePath)
            .AddApplication()
            .AddMyFinanceMcp();

        var app = builder.Build();
        var guard = new LocalRequestGuard(token, loggerFactory.CreateLogger<LocalRequestGuard>());
        app.Use(guard.InvokeAsync);
        app.MapMcp(Path);
        return app;
    }

    private async Task<bool> StopCoreAsync()
    {
        if (_app is not { } app)
        {
            return false;
        }

        _app = null;
        Endpoint = null;
        using (var timeout = new CancellationTokenSource(StopTimeout))
        {
            try
            {
                await app.StopAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // Requisições em andamento são abandonadas após o tempo limite.
            }
        }

        await app.DisposeAsync();
        McpLog.ServerStopped(_logger);
        return true;
    }

    /// <summary>O servidor vive dentro do aplicativo: não reage a Ctrl+C nem encerra o processo.</summary>
    private sealed class EmbeddedLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
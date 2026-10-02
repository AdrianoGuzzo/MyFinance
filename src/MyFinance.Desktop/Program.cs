using System.Globalization;
using System.Reflection;

using Avalonia;
using Avalonia.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MyFinance.Application;
using MyFinance.Application.Common.Logging;
using MyFinance.Desktop.Services;
using MyFinance.Infrastructure;
using MyFinance.Infrastructure.Logging;
using MyFinance.Mcp;

using Serilog;

namespace MyFinance.Desktop;

internal static partial class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = "MyFinance",
        });

        ApplyCulture(builder.Configuration["App:Culture"]);

        var paths = AppPaths.From(builder.Configuration);
        builder.Services.AddSerilog((_, logger) => SerilogSetup.Configure(logger, builder.Configuration, paths.LogDirectory));
        builder.Services
            .AddSingleton(paths)
            .AddInfrastructure(paths.DatabasePath)
            .AddApplication()
            .AddDesktop();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MyFinance.Desktop");
        RegisterGlobalExceptionHandlers(logger);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        LogApplicationStarted(logger, version, builder.Environment.EnvironmentName);

        try
        {
            return BuildAvaloniaApp(() => new App(host.Services)).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogUnhandledException(logger, ex);
            return 1;
        }
        finally
        {
            StopMcpServer(host.Services);
        }
    }

    /// <summary>Libera a porta do servidor MCP antes de descartar os serviços (o descarte do contêiner é síncrono).</summary>
    private static void StopMcpServer(IServiceProvider services)
    {
        var server = services.GetRequiredService<McpServerHost>();
        if (server.IsRunning)
        {
            Task.Run(() => server.StopAsync(CancellationToken.None)).Wait(TimeSpan.FromSeconds(5));
        }
    }

    // Usado pelo designer visual (App sem serviços).
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(() => new App());

    private static AppBuilder BuildAvaloniaApp(Func<App> appFactory)
        => AppBuilder.Configure(appFactory)
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void ApplyCulture(string? name)
    {
        var culture = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(name) ? "pt-BR" : name);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Último recurso: registra no log. Erros de telas já são tratados em <c>PageViewModel.RunAsync</c>.</summary>
    private static void RegisterGlobalExceptionHandlers(Microsoft.Extensions.Logging.ILogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogUnhandledException(logger, ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogUnhandledException(logger, e.Exception);
            e.SetObserved();
        };

        // Exceções no thread de UI não encerram a aplicação.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            LogUnhandledException(logger, e.Exception);
            e.Handled = true;
        };
    }

    [LoggerMessage(EventId = LogEvents.ApplicationStarted, EventName = nameof(LogEvents.ApplicationStarted), Level = LogLevel.Information,
        Message = "MyFinance {Version} iniciado (ambiente {Environment})")]
    private static partial void LogApplicationStarted(Microsoft.Extensions.Logging.ILogger logger, string version, string environment);

    [LoggerMessage(EventId = LogEvents.UnhandledException, EventName = nameof(LogEvents.UnhandledException), Level = LogLevel.Critical,
        Message = "Exceção não tratada")]
    private static partial void LogUnhandledException(Microsoft.Extensions.Logging.ILogger logger, Exception exception);
}
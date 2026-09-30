using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MyFinance.Infrastructure;
using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Application.Tests;

/// <summary>Relógio fixo em 30/09/2026 12:00 (UTC, que também é o fuso "local" nos testes).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// Composição real da aplicação (AddApplication + repositórios + importadores) sobre SQLite em memória
/// criado pelas migrations. Cada <see cref="Get{T}"/> usa um escopo novo, como uma tela faria.
/// </summary>
internal sealed class TestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private TestHost(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    public FixedTimeProvider Clock => (FixedTimeProvider)_provider.GetRequiredService<TimeProvider>();

    /// <param name="configure">Registros adicionais aplicados antes de <c>AddApplication</c> (que só usa TryAdd para portas substituíveis).</param>
    public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken, Action<IServiceCollection>? configure = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)))
            .AddDbContext<FinanceDbContext>(o => o.UseSqlite(connection))
            .AddPersistenceServices()
            .AddImporters();
        configure?.Invoke(services);
        services.AddApplication();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var host = new TestHost(connection, provider);
        await host.Get<DatabaseInitializer>().InitializeAsync(cancellationToken);
        return host;
    }

    public IServiceScope CreateScope()
    {
        var scope = _provider.CreateScope();
        _scopes.Add(scope);
        return scope;
    }

    /// <summary>Resolve o serviço em um escopo novo (o escopo vive até o fim do teste).</summary>
    public T Get<T>()
        where T : notnull
    {
        return CreateScope().ServiceProvider.GetRequiredService<T>();
    }

    private readonly List<IServiceScope> _scopes = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
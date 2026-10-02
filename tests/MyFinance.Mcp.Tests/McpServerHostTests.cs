using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using MyFinance.Application;
using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Invoices;
using MyFinance.Application.Tests.Privacy;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;
using MyFinance.Infrastructure;
using MyFinance.Infrastructure.Persistence;

namespace MyFinance.Mcp.Tests;

/// <summary>Servidor real (Kestrel em porta livre, banco em arquivo temporário) acessado pelo cliente MCP oficial.</summary>
public sealed class McpServerHostTests : IAsyncLifetime
{
    private const string Token = "token-de-teste-0123456789";
    private const string Description = "FARMACIA SAO JOAO";

    private static readonly string[] ExpectedTools =
    [
        "resumo_mes", "gastos_por_categoria", "gastos_por_estabelecimento", "evolucao_mensal",
        "listar_cartoes", "listar_faturas", "listar_parcelamentos",
        "buscar_transacoes", "listar_nao_categorizadas", "categorizar_transacao", "categorizar_transacoes_em_lote",
        "listar_categorias", "listar_regras", "criar_categoria", "criar_regra", "editar_regra", "desativar_regra", "ativar_regra",
        "aplicar_regras_pendentes",
        "obter_estrategia", "simular_cenario", "listar_limites", "listar_recorrentes", "definir_meta", "remover_meta",
        "criar_limite", "alterar_limite", "excluir_limite", "classificar_recorrente", "descartar_recorrente", "restaurar_recorrente",
    ];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"myfinance-mcp-{Guid.NewGuid():N}.db");
    private readonly CapturingLoggerProvider _logs = new();
    private readonly DataChangeNotifier _notifier = new();
    private ILoggerFactory? _loggerFactory;
    private McpServerHost? _host;
    private Guid _transactionId;
    private int _changes;

    private CancellationToken Ct { get; } = TestContext.Current.CancellationToken;

    private McpServerHost Host => _host ?? throw new InvalidOperationException();

    public async ValueTask InitializeAsync()
    {
        _transactionId = await SeedAsync();
        _notifier.DataChanged += (_, _) => Interlocked.Increment(ref _changes);
        _loggerFactory = LoggerFactory.Create(b => b.AddProvider(_logs).SetMinimumLevel(LogLevel.Trace));
        _host = new McpServerHost(new McpServerHostOptions(_databasePath), _loggerFactory, _notifier, TimeProvider.System);
        await _host.StartAsync(0, Token, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _loggerFactory?.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task Cliente_lista_o_catalogo_fechado_de_ferramentas_e_roteiros()
    {
        await using var client = await ConnectAsync();

        var tools = await client.ListToolsAsync(cancellationToken: Ct);
        var prompts = await client.ListPromptsAsync(cancellationToken: Ct);

        tools.Select(t => t.Name).Should().BeEquivalentTo(ExpectedTools);
        tools.Select(t => t.Name).Should().NotContain(n =>
            n.Contains("importar", StringComparison.Ordinal) || n.Contains("pagar", StringComparison.Ordinal)
            || n.Contains("cartao", StringComparison.Ordinal) && !n.StartsWith("listar", StringComparison.Ordinal));
        prompts.Select(p => p.Name).Should().BeEquivalentTo("categorizar_pendentes", "analisar_mes");
        Host.Endpoint!.Host.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Cliente_busca_e_categoriza_lancamento()
    {
        await using var client = await ConnectAsync();

        var search = await client.CallToolAsync("buscar_transacoes", new Dictionary<string, object?> { ["texto"] = "farmacia" }, cancellationToken: Ct);
        var categories = await client.CallToolAsync("listar_categorias", cancellationToken: Ct);
        var health = JsonDocument.Parse(Text(categories)).RootElement.EnumerateArray()
            .Single(c => c.GetProperty("nome").GetString() == "Saúde").GetProperty("id").GetGuid();
        var categorize = await client.CallToolAsync(
            "categorizar_transacao", new Dictionary<string, object?> { ["transacaoId"] = _transactionId, ["categoriaId"] = health }, cancellationToken: Ct);

        Text(categories).Should().Contain("\"Saúde\"", "acentos sem escape economizam tokens");
        search.IsError.Should().NotBe(true);
        var page = JsonDocument.Parse(Text(search)).RootElement;
        page.GetProperty("total").GetInt32().Should().Be(1);
        page.GetProperty("itens")[0].GetProperty("tipo").GetString().Should().Be("Purchase");
        page.GetProperty("itens")[0].GetProperty("gasto").GetDecimal().Should().Be(1234.56m);
        categorize.IsError.Should().NotBe(true);
        _changes.Should().Be(1);
    }

    [Fact]
    public async Task Erro_de_validacao_chega_com_mensagem_amigavel()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "categorizar_transacao",
            new Dictionary<string, object?> { ["transacaoId"] = _transactionId, ["categoriaId"] = Guid.CreateVersion7() },
            cancellationToken: Ct);
        var badMonth = await client.CallToolAsync("resumo_mes", new Dictionary<string, object?> { ["mes"] = "setembro" }, cancellationToken: Ct);

        result.IsError.Should().BeTrue();
        Text(result).Should().Contain("Categoria não encontrada.");
        badMonth.IsError.Should().BeTrue();
        Text(badMonth).Should().Contain("aaaa-MM");
        _changes.Should().Be(0);
    }

    [Fact]
    public async Task Requisicao_sem_token_ou_com_token_errado_e_recusada()
    {
        using var http = new HttpClient();

        using var withoutToken = await http.SendAsync(Request(token: null), Ct);
        using var wrongToken = await http.SendAsync(Request(token: "outro"), Ct);

        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Host_ou_origem_externos_sao_recusados()
    {
        using var http = new HttpClient();
        var foreignHost = Request(Token);
        foreignHost.Headers.Host = "evil.example";
        var foreignOrigin = Request(Token);
        foreignOrigin.Headers.Add("Origin", "https://evil.example");
        var localOrigin = Request(Token);
        localOrigin.Headers.Add("Origin", "http://localhost:3000");

        using var hostResponse = await http.SendAsync(foreignHost, Ct);
        using var originResponse = await http.SendAsync(foreignOrigin, Ct);
        using var localResponse = await http.SendAsync(localOrigin, Ct);

        hostResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        originResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        localResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Porta_em_uso_gera_erro_amigavel_e_parar_libera_a_porta()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var busyPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var other = new McpServerHost(new McpServerHostOptions(_databasePath), _loggerFactory!, _notifier, TimeProvider.System);

        var act = () => other.StartAsync(busyPort, Token, Ct);

        (await act.Should().ThrowAsync<McpServerStartException>()).Which.Message.Should().Contain(busyPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        other.IsRunning.Should().BeFalse();
        listener.Stop();

        var port = Host.Endpoint!.Port;
        await Host.StopAsync(Ct);
        Host.IsRunning.Should().BeFalse();
        using var probe = new TcpListener(IPAddress.Loopback, port);
        probe.Start(); // não lança: a porta foi liberada
        probe.Stop();
    }

    [Fact]
    public async Task Logs_do_servidor_nao_contem_dados_financeiros_nem_o_token()
    {
        await using (var client = await ConnectAsync())
        {
            await client.CallToolAsync("buscar_transacoes", new Dictionary<string, object?> { ["texto"] = "farmacia" }, cancellationToken: Ct);
            await client.CallToolAsync("resumo_mes", cancellationToken: Ct);
            await client.CallToolAsync(
                "categorizar_transacoes_em_lote",
                new Dictionary<string, object?> { ["itens"] = new[] { new { transacaoId = _transactionId, categoriaId = Guid.CreateVersion7() } } },
                cancellationToken: Ct);
        }

        using var http = new HttpClient();
        using var _ = await http.SendAsync(Request(token: "token-errado"), Ct);

        _logs.Entries.Should().Contain(e => e.Contains("McpServerStarted", StringComparison.Ordinal), "o teste precisa observar os logs reais");
        _logs.Entries.Should().Contain(e => e.Contains("McpRequestRejected", StringComparison.Ordinal));
        foreach (var sensitive in new[] { Description, "Farmacia", "1234,56", "1234.56", "4321", Token, "token-errado", _transactionId.ToString() })
        {
            _logs.Entries.Should().NotContain(e => e.Contains(sensitive, StringComparison.OrdinalIgnoreCase),
                $"\"{sensitive}\" é dado sensível e não pode ir para o log");
        }
    }

    private async Task<McpClient> ConnectAsync()
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = Host.Endpoint!,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {Token}" },
        }, NullLoggerFactoryInstance);
        return await McpClient.CreateAsync(transport, cancellationToken: Ct);
    }

    private static ILoggerFactory NullLoggerFactoryInstance => Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

    private HttpRequestMessage Request(string? token)
    {
        const string body = """{"jsonrpc":"2.0","id":1,"method":"ping","params":{}}""";
        var request = new HttpRequestMessage(HttpMethod.Post, Host.Endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    /// <summary>Banco em arquivo com um cartão final 4321 e uma compra de R$ 1.234,56 na fatura atual.</summary>
    private async Task<Guid> SeedAsync()
    {
        var services = new ServiceCollection().AddLogging().AddInfrastructure(_databasePath).AddApplication();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<DatabaseInitializer>().InitializeAsync(Ct);
        await sp.GetRequiredService<CategoryService>().EnsureDefaultCategoriesAsync(Ct);
        var cardId = await sp.GetRequiredService<CreditCardService>().CreateAsync(
            new SaveCreditCardCommand("Cartão teste", "Banco", CardBrand.Visa, "4321", 5000m, 3, 10), Ct);

        var card = (await sp.GetRequiredService<ICreditCardRepository>().GetByIdAsync(cardId, Ct))!;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var month = card.GetInvoicePeriod(today).ReferenceMonth;
        var invoices = await InvoiceBook.EnsureAsync(card, [month], sp.GetRequiredService<IInvoiceRepository>(), Ct);
        var transaction = Transaction.Create(invoices[month], today, -1234.56m, Description, TransactionKind.Purchase, DateTime.UtcNow);
        sp.GetRequiredService<ITransactionRepository>().AddRange([transaction]);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return transaction.Id;
    }
}
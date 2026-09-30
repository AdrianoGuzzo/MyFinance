using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;

namespace MyFinance.Application.Tests.Privacy;

/// <summary>
/// Requisito de privacidade: logs não podem conter número do cartão, descrições, valores,
/// nomes de arquivo nem o conteúdo de extratos. Captura tudo o que é registrado (inclusive pelo EF Core)
/// e procura dados sensíveis conhecidos.
/// </summary>
public sealed class LogPrivacyTests : ApplicationTestBase
{
    private const string CardNumber = "5555666677774321";
    private const string FileName = "fatura-joao-da-silva.ofx";

    private static readonly string[] SensitiveValues =
    [
        "55556666", "joao-da-silva", "FARMACIA SAO JOAO", "Farmacia Sao Joao", "SEGREDO-FITID", "1234,56", "1234.56", "4321", "<STMTTRN>",
    ];

    private readonly CapturingLoggerProvider _logs = new();

    private protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<ILoggerProvider>(_logs);

    private static string Ofx() => $"""
        <OFX><CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS>
        <CCACCTFROM><ACCTID>{CardNumber}</ACCTID></CCACCTFROM>
        <BANKTRANLIST>
        <STMTTRN><DTPOSTED>20260910<TRNAMT>-1234.56<FITID>SEGREDO-FITID<MEMO>Farmacia Sao Joao</STMTTRN>
        </BANKTRANLIST>
        </CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1></OFX>
        """;

    [Fact]
    public async Task Importacoes_nao_registram_dados_financeiros_nos_logs()
    {
        var cardId = await CreateCardAsync(lastFour: "4321");
        var service = Host.Get<ImportService>();

        for (var i = 0; i < 2; i++) // segunda vez: arquivo já importado, tudo duplicado
        {
            var analysis = await service.AnalyzeAsync(FileName, Text(Ofx()), Ct);
            await service.ConfirmAsync(await service.PreviewAsync(analysis, cardId, null, Ct), Ct);
        }

        var rejected = () => service.AnalyzeAsync("joao-da-silva.csv", Text("coluna;x\nFARMACIA SAO JOAO;1234,56"), Ct);
        await rejected.Should().ThrowAsync<ImportException>();

        _logs.Entries.Should().Contain(e => e.Contains("ImportCompleted", StringComparison.Ordinal), "o teste precisa observar os logs reais");
        foreach (var sensitive in SensitiveValues)
        {
            _logs.Entries.Should().NotContain(e => e.Contains(sensitive, StringComparison.OrdinalIgnoreCase),
                $"\"{sensitive}\" é dado sensível e não pode ir para o log");
        }
    }

    /// <summary>Guarda mensagem formatada, propriedades estruturadas e exceção de cada registro.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyCollection<string> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new Logger(_entries);

        public void Dispose() { }

        private sealed class Logger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
                    : string.Empty;
                entries.Enqueue($"{eventId.Name} {formatter(state, exception)} {properties} {exception}");
            }
        }
    }
}
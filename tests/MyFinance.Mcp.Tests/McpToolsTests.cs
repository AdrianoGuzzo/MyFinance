using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol;

using MyFinance.Application.Categories;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Dashboard;
using MyFinance.Application.Reports;
using MyFinance.Application.Strategy;
using MyFinance.Application.Tests;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Mcp.Tools;

namespace MyFinance.Mcp.Tests;

/// <summary>Ferramentas chamadas diretamente, sobre a composição real da aplicação.</summary>
public sealed class McpToolsTests : ApplicationTestBase
{
    private readonly DataChangeNotifier _notifier = new();
    private int _changes;

    public McpToolsTests() => _notifier.DataChanged += (_, _) => Interlocked.Increment(ref _changes);

    private protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton(_notifier);

    [Fact]
    public async Task Resumo_mes_usa_mes_aaaa_MM_e_gasto_positivo()
    {
        var card = await CreateCardAsync();
        await SpendAsync(card, Month(10), 120m, "Mercado Extra", "Supermercado");

        var resumo = await ResumoTools.ResumoMesAsync(Host.Get<DashboardService>(), "2026-10", cancellationToken: Ct);

        resumo.Mes.Should().Be("2026-10");
        resumo.GastoMes.Should().Be(120m);
        resumo.Categorias.Should().ContainSingle().Which.Nome.Should().Be("Supermercado");
        resumo.Evolucao.Should().HaveCount(6).And.OnlyContain(e => e.Mes.Length == 7);
    }

    [Fact]
    public async Task Gastos_por_categoria_traz_o_id_da_categoria()
    {
        var card = await CreateCardAsync();
        await SpendAsync(card, Month(10), 120m, "Mercado Extra", "Supermercado");
        await SpendAsync(card, Month(10), 30m, "Banca");

        var report = await ResumoTools.GastosPorCategoriaAsync(Host.Get<ReportService>(), "2026-10", "2026-10", cancellationToken: Ct);

        report.De.Should().Be("2026-10");
        report.Itens.Select(i => (i.CategoriaId, i.Total)).Should().Equal(
            (await CategoryIdAsync("Supermercado"), 120m), ((Guid?)null, 30m));
    }

    [Fact]
    public async Task Listar_nao_categorizadas_agrupa_por_estabelecimento_e_ignora_pagamentos()
    {
        var card = await CreateCardAsync();
        var a = await AddTransactionAsync(card, Day(5), -50m, "UBER *TRIP 12345");
        var b = await AddTransactionAsync(card, Day(6), -30m, "UBER *TRIP 98765");
        await AddTransactionAsync(card, Day(7), -10m, "Padaria");
        await AddTransactionAsync(card, Day(8), 500m, "Pagamento recebido");
        await AddTransactionAsync(card, Day(9), -99m, "Cinema", "Lazer");

        var pending = await TransacaoTools.ListarNaoCategorizadasAsync(Host.Get<TransactionService>(), cancellationToken: Ct);

        pending.TotalPendentes.Should().Be(3);
        pending.Grupos.Should().HaveCount(2);
        var uber = pending.Grupos![0];
        uber.Quantidade.Should().Be(2);
        uber.Total.Should().Be(80m);
        uber.TransacaoIds.Should().BeEquivalentTo([a, b]);
        uber.Exemplos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Categorizar_em_lote_grava_e_avisa_a_interface()
    {
        var card = await CreateCardAsync();
        var id = await AddTransactionAsync(card, Day(5), -50m, "Cinema");
        var leisure = await CategoryIdAsync("Lazer");

        var result = await TransacaoTools.CategorizarTransacoesEmLoteAsync(
            Host.Get<TransactionService>(), _notifier, [new ItemCategorizacao(id, leisure)], Ct);

        result.Should().Be(new LoteResultado(1, 0));
        _changes.Should().Be(1);
        var search = await TransacaoTools.BuscarTransacoesAsync(Host.Get<TransactionService>(), categoriaId: leisure, cancellationToken: Ct);
        search.Itens.Should().ContainSingle().Which.Categoria.Should().Be("Lazer");
    }

    [Fact]
    public async Task Categorizar_em_lote_com_categoria_inexistente_nao_grava_nem_avisa()
    {
        var card = await CreateCardAsync();
        var id = await AddTransactionAsync(card, Day(5), -50m, "Cinema");

        var act = () => TransacaoTools.CategorizarTransacoesEmLoteAsync(
            Host.Get<TransactionService>(), _notifier, [new ItemCategorizacao(id, Guid.CreateVersion7())], Ct);

        await act.Should().ThrowAsync<ValidationException>();
        _changes.Should().Be(0);
        (await TransacaoTools.ListarNaoCategorizadasAsync(Host.Get<TransactionService>(), cancellationToken: Ct)).TotalPendentes.Should().Be(1);
    }

    [Fact]
    public async Task Categorizar_transacao_devolve_sugestao_de_regra()
    {
        var card = await CreateCardAsync();
        var first = await AddTransactionAsync(card, Day(5), -20m, "PADARIA DO ZE");
        await AddTransactionAsync(card, Day(6), -25m, "PADARIA DO ZE");
        var food = await CategoryIdAsync("Alimentação");

        var result = await TransacaoTools.CategorizarTransacaoAsync(Host.Get<TransactionService>(), _notifier, first, food, Ct);

        result.SugestaoRegra.Should().NotBeNull();
        result.SugestaoRegra!.Padrao.Should().Be("PADARIA DO ZE");
        result.SugestaoRegra.PendentesQueCasariam.Should().Be(1);
    }

    [Fact]
    public async Task Criar_regra_e_aplicar_pendentes_categoriza()
    {
        var card = await CreateCardAsync();
        await AddTransactionAsync(card, Day(5), -20m, "ACADEMIA FORMA");
        var health = await CategoryIdAsync("Saúde");

        await CategoriaTools.CriarRegraAsync(Host.Get<CategoryRuleService>(), _notifier, "ACADEMIA", health, cancellationToken: Ct);
        var applied = await CategoriaTools.AplicarRegrasPendentesAsync(Host.Get<CategoryRuleService>(), _notifier, Ct);

        applied.Categorizadas.Should().Be(1);
        _changes.Should().Be(2);
        (await CategoriaTools.ListarRegrasAsync(Host.Get<CategoryRuleService>(), Ct)).Should().Contain(r => r.Padrao == "ACADEMIA");
    }

    [Fact]
    public async Task Criar_subcategoria_devolve_nome_completo()
    {
        var parent = await CategoryIdAsync("Lazer");

        var created = await CategoriaTools.CriarCategoriaAsync(Host.Get<CategoryService>(), _notifier, "Shows", parent, cancellationToken: Ct);

        created.NomeCompleto.Should().Be("Lazer > Shows");
        (await CategoriaTools.ListarCategoriasAsync(Host.Get<CategoryService>(), cancellationToken: Ct))
            .Should().Contain(c => c.Id == created.Id && c.CategoriaPaiId == parent);
    }

    [Fact]
    public async Task Limite_duplicado_gera_erro_amigavel()
    {
        var leisure = await CategoryIdAsync("Lazer");
        await EstrategiaTools.CriarLimiteAsync(Host.Get<SpendingLimitService>(), _notifier, leisure, 300m, Ct);

        var act = () => EstrategiaTools.CriarLimiteAsync(Host.Get<SpendingLimitService>(), _notifier, leisure, 400m, Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Já existe um limite*");
        (await EstrategiaTools.ListarLimitesAsync(Host.Get<SpendingLimitService>(), cancellationToken: Ct)).Should().ContainSingle()
            .Which.ValorMensal.Should().Be(300m);
    }

    [Fact]
    public async Task Meta_e_simulacao()
    {
        var card = await CreateCardAsync();
        await SpendAsync(card, Month(10), 1000m, "Mercado", "Supermercado");
        var market = await CategoryIdAsync("Supermercado");

        await EstrategiaTools.DefinirMetaAsync(Host.Get<StrategyService>(), _notifier, "Viagem", 200m, Ct);
        var strategy = await EstrategiaTools.ObterEstrategiaAsync(Host.Get<StrategyService>(), Ct);
        var scenario = await EstrategiaTools.SimularCenarioAsync(Host.Get<StrategyService>(), [new ReducaoCategoria(market, 0.1m)], Ct);

        strategy.Meta!.ValorMensal.Should().Be(200m);
        scenario.EconomiaMensal.Should().Be(100m);
    }

    [Fact]
    public async Task Simulacao_com_percentual_fora_da_faixa_falha()
    {
        var act = () => EstrategiaTools.SimularCenarioAsync(Host.Get<StrategyService>(), [new ReducaoCategoria(Guid.CreateVersion7(), 1.5m)], Ct);

        await act.Should().ThrowAsync<McpException>().WithMessage("*entre 0 e 1*");
    }

    [Fact]
    public async Task Leituras_nao_avisam_a_interface()
    {
        var card = await CreateCardAsync();
        await SpendAsync(card, Month(10), 10m, "Mercado");

        await ResumoTools.ResumoMesAsync(Host.Get<DashboardService>(), cancellationToken: Ct);
        await TransacaoTools.BuscarTransacoesAsync(Host.Get<TransactionService>(), tipo: TransactionKind.Purchase, cancellationToken: Ct);
        await CategoriaTools.ListarCategoriasAsync(Host.Get<CategoryService>(), cancellationToken: Ct);

        _changes.Should().Be(0);
    }

    [Theory]
    [InlineData("2026-09", 2026, 9)]
    [InlineData(" 2026-09-15 ", 2026, 9)]
    public void ParseMonth_aceita_mes_ou_data(string value, int year, int month) =>
        McpArgs.ParseMonth(value, "mes").Should().Be(new DateOnly(year, month, 1));

    [Theory]
    [InlineData("09/2026")]
    [InlineData("setembro")]
    public void ParseMonth_invalido_gera_erro_para_a_IA(string value)
    {
        var act = () => McpArgs.ParseMonth(value, "mes");

        act.Should().Throw<McpException>().WithMessage("*'mes'*aaaa-MM*");
    }
}
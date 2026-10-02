using MyFinance.Application.Dashboard;
using MyFinance.Application.Imports;
using MyFinance.Application.Installments;
using MyFinance.Application.Reports;
using MyFinance.Application.Tests.Imports;
using MyFinance.Domain.Analysis;

namespace MyFinance.Application.Tests.Dashboard;

/// <summary>
/// Hoje: 30/09/2026. Cartão fecha dia 3 e vence dia 10: a fatura aberta (atual) é a de outubro.
/// Histórico: julho R$ 400, agosto R$ 700, setembro R$ 790.
/// </summary>
public abstract class SpendingScenario : ApplicationTestBase
{
    protected async Task<Guid> SeedHistoryAsync()
    {
        var card = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await SpendAsync(card, Month(7), 400m, "Mercado", "Supermercado");
        await SpendAsync(card, Month(8), 500m, "Mercado", "Supermercado");
        await SpendAsync(card, Month(8), 200m, "Cinema", "Lazer");
        await SpendAsync(card, Month(9), 600m, "Mercado", "Supermercado");
        await SpendAsync(card, Month(9), 100m, "Cinema", "Lazer");
        await SpendAsync(card, Month(9), 90m, "Cantina", "Alimentação > Restaurantes");
        await SpendAsync(card, Month(10), 300m, "Mercado", "Supermercado");
        await SpendAsync(card, Month(10), 400m, "Show", "Lazer");
        await SpendAsync(card, Month(10), 89m, "iFood", "Alimentação > Delivery");
        await SpendAsync(card, Month(10), 50m, "Loja sem categoria");
        await AddTransactionAsync(card, Day(20), 20m, "Estorno show", "Lazer");
        await AddTransactionAsync(card, Day(21), 1000m, "Pagamento recebido");
        return card;
    }

    protected async Task ImportInstallmentsAsync(Guid card)
    {
        var service = Host.Get<ImportService>();
        var ofx = ImportServiceTests.Ofx("1234",
            ("20260910", "-500.00", "N2", "Notebook - Parcela 2/10"),
            ("20260912", "-300.00", "T1", "TV - Parcela 1/3"));
        var analysis = await service.AnalyzeAsync("outubro.ofx", Text(ofx), Ct);
        await service.ConfirmAsync(await service.PreviewAsync(analysis, card, null, Ct), Ct);
    }
}

public sealed class DashboardServiceTests : SpendingScenario
{
    private Task<DashboardDto> GetAsync(DateOnly? month = null) => Host.Get<DashboardService>().GetAsync(month, 6, Ct);

    [Fact]
    public async Task Dashboard_vazio()
    {
        var dashboard = await GetAsync();

        dashboard.ReferenceMonth.Should().Be(Month(9));
        dashboard.MonthSpending.Should().Be(0);
        dashboard.Average.Should().BeNull();
        dashboard.Variation.Should().BeNull();
        dashboard.Categories.Should().BeEmpty();
        dashboard.Evolution.Should().HaveCount(6);
        dashboard.Insights.Should().BeEmpty();
    }

    [Fact]
    public async Task Gastos_do_mes_media_e_variacao_pela_competencia_da_fatura()
    {
        await SeedHistoryAsync();

        var dashboard = await GetAsync();

        dashboard.ReferenceMonth.Should().Be(Month(10), "a fatura aberta é a de outubro");
        dashboard.IsPartial.Should().BeTrue();
        dashboard.MonthSpending.Should().Be(819m, "estorno reduz o gasto; pagamento da fatura não conta");
        dashboard.HistoryMonths.Should().Be(3);
        dashboard.Average.Should().Be(630m);
        dashboard.Variation.Should().Be(0.3m);
        dashboard.CurrentInvoiceTotal.Should().Be(819m);
        dashboard.Evolution.Select(m => m.Amount).Should().Equal(0m, 0m, 400m, 700m, 790m, 819m);
    }

    [Fact]
    public async Task Distribuicao_por_categoria_com_comparacoes()
    {
        await SeedHistoryAsync();

        var dashboard = await GetAsync();

        dashboard.Categories.Select(c => (c.Name, c.Amount)).Should().Equal(
            ("Lazer", 380m), ("Supermercado", 300m), ("Alimentação", 89m), ("Sem categoria", 50m));
        dashboard.Categories.Sum(c => c.Percent).Should().BeApproximately(1m, 0.000001m);
        dashboard.Categories[0].VersusPreviousMonth.Should().Be(2.8m);
        dashboard.Categories[1].VersusAverage.Should().Be(-0.4m);
    }

    [Fact]
    public async Task O_que_aumentou_e_o_que_diminuiu()
    {
        await SeedHistoryAsync();

        var dashboard = await GetAsync();

        dashboard.IncreasedVsPreviousMonth.Select(v => (v.Name, v.Difference)).Should().Equal(("Lazer", 280m), ("Sem categoria", 50m));
        dashboard.DecreasedVsPreviousMonth.Select(v => (v.Name, v.Difference)).Should().Equal(("Supermercado", -300m));
        dashboard.IncreasedVsAverage.Select(v => (v.Name, v.Difference)).Should().Equal(("Lazer", 280m), ("Alimentação", 59m), ("Sem categoria", 50m));
        dashboard.DecreasedVsAverage.Select(v => v.Name).Should().Equal("Supermercado");
    }

    [Fact]
    public async Task Insights_sao_baseados_nos_dados()
    {
        await SeedHistoryAsync();

        var dashboard = await GetAsync();

        dashboard.Insights.Select(i => i.Message).Should().Contain(
        [
            "Seu gasto com Lazer aumentou 280% em relação à média dos últimos 3 meses.",
            "O maior aumento de gastos neste mês ocorreu na categoria Lazer.",
            "Seu gasto médio mensal com Supermercado nos últimos 3 meses foi de R$ 500,00.",
        ]);
        dashboard.Insights.Should().Contain(i => i.Tone == InsightTone.Positive && i.Message.Contains("Supermercado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mes_escolhido_e_mes_fechado()
    {
        await SeedHistoryAsync();

        var september = await GetAsync(Day(15));

        september.ReferenceMonth.Should().Be(Month(9));
        september.IsPartial.Should().BeFalse();
        september.MonthSpending.Should().Be(790m);
        september.Average.Should().Be(550m);
    }

    [Fact]
    public async Task Proximas_faturas_somam_lancamentos_e_parcelas_comprometidas()
    {
        var card = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await ImportInstallmentsAsync(card);

        var dashboard = await GetAsync();

        dashboard.NextInvoices.Select(i => (i.Month, i.Posted, i.Installments, i.Total)).Should().Equal(
            (Month(10), 800m, 0m, 800m),
            (Month(11), 0m, 800m, 800m),
            (Month(12), 0m, 800m, 800m),
            (Month(1, 2027), 0m, 500m, 500m));
        dashboard.OutstandingInstallments.Should().Be(4600m);
        dashboard.Insights.Select(i => i.Message).Should().Contain("Você possui R$ 4.600,00 em compras parceladas ainda não finalizadas.");
    }
}

public sealed class InstallmentServiceTests : SpendingScenario
{
    [Fact]
    public async Task Lista_compras_parceladas_com_parcela_atual_e_valor_restante()
    {
        var card = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await ImportInstallmentsAsync(card);

        var list = await Host.Get<InstallmentService>().ListAsync(includeFinished: false, Ct);

        list.Select(p => (p.Description, p.InstallmentCount, p.InstallmentAmount, p.CurrentNumber, p.RemainingCount, p.RemainingAmount, p.TotalAmount))
            .Should().Equal(
                ("Notebook", 10, 500m, (int?)2, 8, 4000m, 5000m),
                ("TV", 3, 300m, (int?)1, 2, 600m, 900m));
        list[0].LastInvoiceMonth.Should().Be(Month(6, 2027));
    }

    [Fact]
    public async Task Comprometimento_das_proximas_faturas()
    {
        var card = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await ImportInstallmentsAsync(card);

        var commitments = await Host.Get<InstallmentService>().GetCommitmentsAsync(4, Ct);

        commitments.Should().Equal(
            new CommitmentDto(Month(10), 0m, 0),
            new CommitmentDto(Month(11), 800m, 2),
            new CommitmentDto(Month(12), 800m, 2),
            new CommitmentDto(Month(1, 2027), 500m, 1));
    }
}

public sealed class ReportServiceTests : SpendingScenario
{
    private ReportService Service => Host.Get<ReportService>();

    [Fact]
    public async Task Gastos_por_categoria_no_periodo()
    {
        await SeedHistoryAsync();

        var rows = await Service.ByCategoryAsync(Month(9), Month(10), CategoryLevel.Root, Ct);

        rows.Select(r => (r.Name, r.Total, r.MonthlyAverage)).Should().Equal(
            ("Supermercado", 900m, 450m), ("Lazer", 480m, 240m), ("Alimentação", 179m, 89.5m), ("Sem categoria", 50m, 25m));
        rows.Sum(r => r.Percent).Should().BeApproximately(1m, 0.000001m);
        rows[0].Variation.Should().Be(-0.5m);
        rows[0].CategoryId.Should().Be(await CategoryIdAsync("Supermercado"));
        rows[^1].CategoryId.Should().BeNull("\"Sem categoria\" não tem id");
    }

    [Fact]
    public async Task Gastos_por_estabelecimento()
    {
        await SeedHistoryAsync();

        var rows = await Service.ByMerchantAsync(Month(7), Month(10), Ct);

        rows[0].Should().Be(new MerchantReportRow("Mercado", 4, 1800m, 450m));
        rows.Should().NotContain(r => r.Merchant.Contains("Pagamento", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Evolucao_mensal_com_media_e_variacao()
    {
        await SeedHistoryAsync();

        var rows = await Service.EvolutionAsync(Month(7), Month(10), Ct);

        rows.Should().Equal(
            new MonthlyReportRow(Month(7), 400m, null, null),
            new MonthlyReportRow(Month(8), 700m, 400m, 0.75m),
            new MonthlyReportRow(Month(9), 790m, 550m, 90m / 700m),
            new MonthlyReportRow(Month(10), 819m, 630m, 29m / 790m));
    }

    [Fact]
    public async Task Periodo_invertido_gera_erro_de_validacao()
    {
        var act = () => Service.EvolutionAsync(Month(10), Month(7), Ct);

        await act.Should().ThrowAsync<Common.Exceptions.ValidationException>();
    }
}
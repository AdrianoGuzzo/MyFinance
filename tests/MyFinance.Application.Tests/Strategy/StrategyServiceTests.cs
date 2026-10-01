using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Dashboard;
using MyFinance.Application.Strategy;
using MyFinance.Application.Tests.Dashboard;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Enums;

namespace MyFinance.Application.Tests.Strategy;

public sealed class SpendingLimitServiceTests : SpendingScenario
{
    private SpendingLimitService Service => Host.Get<SpendingLimitService>();

    [Fact]
    public async Task Limites_dentro_proximo_e_excedido_na_fatura_atual()
    {
        await SeedHistoryAsync();
        await Service.CreateAsync(new SaveSpendingLimitCommand(await CategoryIdAsync("Lazer"), 300m), Ct);
        await Service.CreateAsync(new SaveSpendingLimitCommand(await CategoryIdAsync("Supermercado"), 350m), Ct);
        await Service.CreateAsync(new SaveSpendingLimitCommand(await CategoryIdAsync("Alimentação"), 800m), Ct);

        var limits = await Service.ListAsync(null, Ct);

        limits.Select(l => (l.CategoryName, l.Used, l.Status)).Should().Equal(
            ("Lazer", 380m, LimitStatus.Exceeded),
            ("Supermercado", 300m, LimitStatus.Near),
            ("Alimentação", 89m, LimitStatus.Within));
        limits[0].Excess.Should().Be(80m);
        limits[2].Available.Should().Be(711m, "a categoria principal inclui as subcategorias (Delivery)");
    }

    [Fact]
    public async Task Limite_de_um_mes_escolhido()
    {
        await SeedHistoryAsync();
        await Service.CreateAsync(new SaveSpendingLimitCommand(await CategoryIdAsync("Supermercado"), 550m), Ct);

        (await Service.ListAsync(Month(9), Ct)).Single().Should().Match<SpendingLimitDto>(l => l.Used == 600m && l.Status == LimitStatus.Exceeded);
    }

    [Fact]
    public async Task Um_limite_por_categoria_e_operacoes_de_manutencao()
    {
        var leisure = await CategoryIdAsync("Lazer");
        var id = await Service.CreateAsync(new SaveSpendingLimitCommand(leisure, 300m), Ct);

        var duplicate = () => Service.CreateAsync(new SaveSpendingLimitCommand(leisure, 500m), Ct);
        await duplicate.Should().ThrowAsync<ValidationException>().WithMessage("Já existe um limite para \"Lazer\"*");

        await Service.ChangeAmountAsync(id, 450m, Ct);
        await Service.DeactivateAsync(id, Ct);
        (await Service.ListAsync(null, Ct)).Single().Should().Match<SpendingLimitDto>(l => l.MonthlyAmount == 450m && !l.IsActive);

        await Service.DeleteAsync(id, Ct);
        (await Service.ListAsync(null, Ct)).Should().BeEmpty();
    }
}

public sealed class RecurringExpenseServiceTests : SpendingScenario
{
    private RecurringExpenseService Service => Host.Get<RecurringExpenseService>();

    private async Task SeedNetflixAsync(Guid card)
    {
        foreach (var month in new[] { 7, 8, 9, 10 })
        {
            await SpendAsync(card, Month(month), 55.90m, "Netflix.com", "Assinaturas");
        }
    }

    [Fact]
    public async Task Detecta_assinatura_com_totais_mensal_e_anual()
    {
        await SeedNetflixAsync(await SeedHistoryAsync());

        var overview = await Service.GetAsync(includeDismissed: false, Ct);

        var netflix = overview.Items.Should().ContainSingle().Subject;
        netflix.Should().Match<RecurringExpenseDto>(r =>
            r.Name == "Netflix.com" && r.MonthlyAmount == 55.90m && r.AnnualAmount == 670.80m
            && r.CategoryName == "Assinaturas" && r.Classification == RecurringClassification.Unclassified && r.MonthsSeen == 4);
        overview.MonthlyTotal.Should().Be(55.90m);
        overview.AnnualTotal.Should().Be(670.80m);
        overview.OptionalMonthly.Should().Be(0m);
    }

    [Fact]
    public async Task Classificacao_e_preservada_e_descartar_remove_dos_totais()
    {
        await SeedNetflixAsync(await SeedHistoryAsync());
        var id = (await Service.GetAsync(false, Ct)).Items.Single().Id;

        await Service.ClassifyAsync(id, RecurringClassification.Optional, Ct);
        var classified = await Service.GetAsync(false, Ct);
        classified.Items.Single().Classification.Should().Be(RecurringClassification.Optional);
        classified.OptionalMonthly.Should().Be(55.90m);

        await Service.DismissAsync(id, Ct);
        var dismissed = await Service.GetAsync(false, Ct);
        dismissed.Items.Should().BeEmpty();
        dismissed.MonthlyTotal.Should().Be(0m);
        (await Service.GetAsync(true, Ct)).Items.Single().IsDismissed.Should().BeTrue();

        await Service.RestoreAsync(id, Ct);
        (await Service.GetAsync(false, Ct)).Items.Should().ContainSingle();
    }
}

public sealed class StrategyServiceTests : SpendingScenario
{
    private StrategyService Service => Host.Get<StrategyService>();

    [Fact]
    public async Task Oportunidades_sem_meta()
    {
        await SeedHistoryAsync();

        var strategy = await Service.GetAsync(Ct);

        strategy.ReferenceMonth.Should().Be(Month(10));
        strategy.Goal.Should().BeNull();
        strategy.Gap.Should().BeNull();
        strategy.HistoryMonths.Should().Be(3);
        var lazer = strategy.Opportunities.Should().ContainSingle().Subject;
        lazer.Should().Match<OpportunityStepDto>(o => o.Title == "Lazer" && o.MonthlyPotential == 280m && o.Source == OpportunitySource.AboveAverage);
        strategy.IdentifiedPotential.Should().Be(280m);
    }

    [Fact]
    public async Task Meta_comparada_com_oportunidades_incluindo_limites_e_recorrentes()
    {
        var card = await SeedHistoryAsync();
        foreach (var month in new[] { 7, 8, 9, 10 })
        {
            await SpendAsync(card, Month(month), 59.90m, "Spotify", "Assinaturas");
        }

        var recurring = Host.Get<RecurringExpenseService>();
        await recurring.ClassifyAsync((await recurring.GetAsync(false, Ct)).Items.Single().Id, RecurringClassification.Evaluate, Ct);
        await Host.Get<SpendingLimitService>().CreateAsync(new SaveSpendingLimitCommand(await CategoryIdAsync("Supermercado"), 200m), Ct);
        await Service.SetGoalAsync("Economizar para viagem", 1000m, Ct);

        var strategy = await Service.GetAsync(Ct);

        strategy.Goal.Should().Be(new GoalDto(strategy.Goal!.Id, "Economizar para viagem", 1000m, 12000m));
        strategy.Opportunities.Select(o => (o.Title, o.MonthlyPotential, o.Cumulative)).Should().Equal(
            ("Lazer", 280m, 280m),
            ("Supermercado", 100m, 380m),
            ("Assinaturas e recorrentes", 59.90m, 439.90m));
        strategy.Gap.Should().Be(560.10m);
        strategy.Opportunities.Should().NotContain(o => o.ReachesGoal);

        await Service.SetGoalAsync("Economizar", 300m, Ct);
        var smaller = await Service.GetAsync(Ct);
        smaller.Gap.Should().Be(0m);
        smaller.Opportunities.Select(o => o.ReachesGoal).Should().Equal(false, true, false);

        await Service.ClearGoalAsync(Ct);
        (await Service.GetAsync(Ct)).Goal.Should().BeNull();
    }

    [Fact]
    public async Task Simulador_usa_a_media_real_dos_meses_anteriores()
    {
        await SeedHistoryAsync();
        var strategy = await Service.GetAsync(Ct);

        strategy.Baselines.Select(b => (b.Name, b.MonthlyAmount)).Should().Equal(("Supermercado", 500m), ("Lazer", 100m), ("Alimentação", 30m));

        var market = strategy.Baselines[0].CategoryId!.Value;
        var leisure = strategy.Baselines[1].CategoryId!.Value;
        var result = await Service.SimulateAsync(new Dictionary<Guid, decimal> { [market] = 0.10m, [leisure] = 0.20m }, Ct);

        result.CurrentMonthly.Should().Be(630m);
        result.MonthlySavings.Should().Be(70m);
        result.AnnualSavings.Should().Be(840m);
        result.ProjectedMonthly.Should().Be(560m);
    }

    [Fact]
    public async Task Dashboard_mostra_oportunidades_e_meta()
    {
        await SeedHistoryAsync();
        await Service.SetGoalAsync("Economizar", 1000m, Ct);

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.Opportunities.Select(o => (o.Title, o.MonthlyPotential)).Should().Equal(("Lazer", 280m));
        dashboard.Goal.Should().Be(new GoalProgressDto("Economizar", 1000m, 280m));
    }
}
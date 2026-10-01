using MyFinance.Domain.Analysis;
using MyFinance.Domain.Exceptions;

using static MyFinance.Domain.Tests.Analysis.AnalysisTestData;

namespace MyFinance.Domain.Tests.Analysis;

public sealed class RecurringExpenseDetectorTests
{
    [Fact]
    public void Detecta_assinatura_mensal_com_valor_estavel()
    {
        SpendingEntry[] entries =
        [
            Spend(5, 55.90m, merchant: "NETFLIX.COM"), Spend(6, 55.90m, merchant: "NETFLIX.COM"),
            Spend(7, 55.90m, merchant: "NETFLIX.COM"), Spend(8, 59.90m, merchant: "NETFLIX.COM"),
            Spend(9, 59.90m, merchant: "NETFLIX.COM"),
        ];

        var detected = RecurringExpenseDetector.Detect(entries, M(9)).Should().ContainSingle().Subject;

        detected.MerchantKey.Should().Be("NETFLIX.COM");
        detected.EstimatedMonthlyAmount.Should().Be(59.90m);
        detected.LastSeenMonth.Should().Be(M(9));
        detected.MonthsSeen.Should().Be(5);
    }

    [Fact]
    public void Ignora_gasto_esporadico_frequente_parcelado_variavel_ou_que_parou()
    {
        var installment = Guid.CreateVersion7();
        SpendingEntry[] entries =
        [
            // Esporádico: 2 meses só.
            Spend(8, 100m, merchant: "LOJA A"), Spend(9, 100m, merchant: "LOJA A"),
            // Várias compras por mês (mercado).
            Spend(7, 50m, merchant: "MERCADO"), Spend(7, 60m, merchant: "MERCADO"), Spend(8, 55m, merchant: "MERCADO"),
            Spend(8, 40m, merchant: "MERCADO"), Spend(9, 70m, merchant: "MERCADO"),
            // Parcela (compra parcelada não é assinatura).
            Spend(7, 500m, merchant: "NOTEBOOK", installmentPurchaseId: installment, installmentNumber: 1),
            Spend(8, 500m, merchant: "NOTEBOOK", installmentPurchaseId: installment, installmentNumber: 2),
            Spend(9, 500m, merchant: "NOTEBOOK", installmentPurchaseId: installment, installmentNumber: 3),
            // Valor muito variável.
            Spend(7, 20m, merchant: "UBER"), Spend(8, 150m, merchant: "UBER"), Spend(9, 60m, merchant: "UBER"),
            // Parou há mais de um mês.
            Spend(4, 30m, merchant: "ACADEMIA"), Spend(5, 30m, merchant: "ACADEMIA"), Spend(6, 30m, merchant: "ACADEMIA"),
        ];

        RecurringExpenseDetector.Detect(entries, M(9)).Should().BeEmpty();
    }
}

public sealed class SavingsOpportunityFinderTests
{
    private static readonly CategoryKey Delivery = Lookup.Resolve(AnalysisTestData.Delivery.Id, CategoryLevel.Leaf);
    private static readonly CategoryKey Leisure = Lookup.Resolve(AnalysisTestData.Leisure.Id, CategoryLevel.Leaf);
    private static readonly CategoryKey Market = Lookup.Resolve(AnalysisTestData.Market.Id, CategoryLevel.Leaf);

    private static readonly RecurringSummary NoRecurring = new(0, 0m, 0, 0m);

    [Fact]
    public void Categoria_acima_da_media_e_uma_oportunidade()
    {
        var input = new OpportunityInput(6, [new(Delivery, 720m, 380m, 340m, 340m / 380m), new(Market, 1000m, 950m, 50m, 50m / 950m)], [], NoRecurring);

        var opportunity = SavingsOpportunityFinder.Find(input).Should().ContainSingle().Subject;

        opportunity.Source.Should().Be(OpportunitySource.AboveAverage);
        opportunity.Title.Should().Be("Alimentação > Delivery");
        opportunity.MonthlyPotential.Should().Be(340m);
        opportunity.Detail.Should().Be("Média dos últimos 6 meses: R$ 380,00. Gasto atual: R$ 720,00 (+89%).");
    }

    [Theory]
    [InlineData(2, 720, 380)]    // pouco histórico
    [InlineData(6, 430, 380)]    // abaixo de +15%
    [InlineData(6, 125, 80)]     // +56%, mas diferença menor que R$ 50
    public void Sem_historico_ou_abaixo_do_limiar_nao_ha_oportunidade(int history, decimal current, decimal average)
    {
        var input = new OpportunityInput(history, [new(Delivery, current, average, current - average, (current - average) / average)], [], NoRecurring);

        SavingsOpportunityFinder.Find(input).Should().BeEmpty();
    }

    [Fact]
    public void Limite_excedido_e_recorrentes_opcionais_entram_ordenados_pelo_potencial()
    {
        var input = new OpportunityInput(
            6,
            [new(Leisure, 620m, 500m, 120m, 0.24m)],
            [new ExceededLimit(Leisure, 400m, 620m)],
            new RecurringSummary(OptionalCount: 3, OptionalMonthly: 100m, TotalCount: 8, TotalMonthly: 287m));

        var opportunities = SavingsOpportunityFinder.Find(input);

        opportunities.Select(o => (o.Source, o.Title, o.MonthlyPotential)).Should().Equal(
            (OpportunitySource.LimitExceeded, "Lazer", 220m),
            (OpportunitySource.Recurring, "Assinaturas e recorrentes", 100m));
    }
}

public sealed class ScenarioAndGoalTests
{
    private static readonly CategoryKey Food = Lookup.Resolve(AnalysisTestData.Food.Id, CategoryLevel.Root);
    private static readonly CategoryKey Leisure = Lookup.Resolve(AnalysisTestData.Leisure.Id, CategoryLevel.Root);
    private static readonly CategoryKey Shopping = new(Guid.CreateVersion7(), "Compras", "#000000");

    [Fact]
    public void Cenario_soma_as_reducoes_sobre_o_historico()
    {
        CategoryBaseline[] baselines = [new(Food, 1200m), new(Leisure, 600m), new(Shopping, 1500m), new(CategoryKey.Uncategorized, 200m)];
        var reductions = new Dictionary<Guid, decimal> { [Food.Id!.Value] = 0.15m, [Leisure.Id!.Value] = 0.20m, [Shopping.Id!.Value] = 0.10m };

        var result = ScenarioSimulator.Simulate(baselines, reductions);

        result.Lines.Select(l => l.MonthlySavings).Should().Equal(180m, 120m, 150m, 0m);
        result.CurrentMonthly.Should().Be(3500m);
        result.MonthlySavings.Should().Be(450m);
        result.AnnualSavings.Should().Be(5400m);
        result.ProjectedMonthly.Should().Be(3050m);
    }

    [Fact]
    public void Reducao_fora_de_0_a_100_por_cento_falha()
    {
        var act = () => ScenarioSimulator.Simulate([new(Food, 100m)], new Dictionary<Guid, decimal> { [Food.Id!.Value] = 1.5m });

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Meta_comparada_com_o_potencial_identificado()
    {
        SavingsOpportunity[] opportunities =
        [
            new(OpportunitySource.AboveAverage, null, "Lazer", "", 180m),
            new(OpportunitySource.AboveAverage, null, "Delivery", "", 250m),
            new(OpportunitySource.Recurring, null, "Assinaturas", "", 100m),
        ];

        var partial = GoalPlanner.Plan(1000m, opportunities);
        partial.IdentifiedPotential.Should().Be(530m);
        partial.Gap.Should().Be(470m);
        partial.IsCovered.Should().BeFalse();
        partial.Steps.Select(s => (s.Opportunity.Title, s.Cumulative)).Should().Equal(("Delivery", 250m), ("Lazer", 430m), ("Assinaturas", 530m));

        var covered = GoalPlanner.Plan(400m, opportunities);
        covered.IsCovered.Should().BeTrue();
        covered.Steps.Select(s => s.ReachesGoal).Should().Equal(false, true, false);
    }
}
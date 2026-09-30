using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;

using static MyFinance.Domain.Tests.Analysis.AnalysisTestData;

namespace MyFinance.Domain.Tests.Analysis;

public sealed class SpendingCalculatorTests
{
    [Fact]
    public void Total_do_mes_soma_compras_desconta_estornos_e_ignora_pagamentos()
    {
        SpendingEntry[] entries = [Spend(9, 100m), Spend(9, 50m), Spend(9, -30m), Payment(9, 1000m), Spend(8, 999m)];

        SpendingCalculator.Total(entries, M(9)).Should().Be(120m);
    }

    [Fact]
    public void Media_mensal_usa_ate_6_meses_anteriores_a_partir_do_primeiro_mes_com_dados()
    {
        SpendingEntry[] entries = [Spend(6, 4850m), Spend(7, 5320m), Spend(8, 6100m), Spend(9, 5740m)];

        var baseline = SpendingCalculator.BaselineMonths(M(9), firstMonth: M(6));

        baseline.Should().Equal(M(6), M(7), M(8));
        SpendingCalculator.Average(entries, baseline).Should().Be(5423.33m);
    }

    [Fact]
    public void Media_conta_meses_sem_gasto_como_zero_e_limita_a_janela()
    {
        SpendingEntry[] entries = [Spend(1, 1200m), Spend(9, 500m)];

        var baseline = SpendingCalculator.BaselineMonths(M(9), firstMonth: M(1));

        baseline.Should().Equal(M(3), M(4), M(5), M(6), M(7), M(8));
        SpendingCalculator.Average(entries, baseline).Should().Be(0m);
    }

    [Fact]
    public void Sem_historico_nao_ha_media_nem_variacao()
    {
        SpendingCalculator.BaselineMonths(M(9), firstMonth: M(9)).Should().BeEmpty();
        SpendingCalculator.BaselineMonths(M(9), firstMonth: null).Should().BeEmpty();
        SpendingCalculator.Average([Spend(9, 10m)], []).Should().BeNull();
        SpendingCalculator.Variation(100m, null).Should().BeNull();
        SpendingCalculator.Variation(100m, 0m).Should().BeNull();
    }

    [Theory]
    [InlineData(5742, 5180, 0.1085)]
    [InlineData(750, 400, 0.875)]
    [InlineData(300, 600, -0.5)]
    public void Variacao_relativa(decimal current, decimal baseline, decimal expected)
    {
        decimal.Round(SpendingCalculator.Variation(current, baseline)!.Value, 4).Should().Be(expected);
    }

    [Fact]
    public void Percentual_por_categoria_soma_100_e_inclui_sem_categoria()
    {
        SpendingEntry[] entries =
        [
            Spend(9, 600m, Delivery), Spend(9, 400m, Restaurants), Spend(9, 500m, Leisure), Spend(9, 600m),
            Spend(9, -100m, Leisure), Payment(9, 3000m), Spend(8, 800m, Market),
        ];

        var shares = SpendingCalculator.ByCategory(entries, M(9), Lookup, CategoryLevel.Root);

        shares.Select(s => (s.Category.Name, s.Amount, s.Percent)).Should().Equal(
            ("Alimentação", 1000m, 0.5m), ("Sem categoria", 600m, 0.3m), ("Lazer", 400m, 0.2m));
        shares.Sum(s => s.Percent).Should().Be(1m);
    }

    [Fact]
    public void Percentual_por_subcategoria_mostra_o_nome_completo()
    {
        SpendingEntry[] entries = [Spend(9, 600m, Delivery), Spend(9, 400m, Restaurants)];

        var shares = SpendingCalculator.ByCategory(entries, M(9), Lookup, CategoryLevel.Leaf);

        shares.Select(s => s.Category.Name).Should().Equal("Alimentação > Delivery", "Alimentação > Restaurantes");
        shares.Sum(s => s.Percent).Should().Be(1m);
    }

    [Fact]
    public void Serie_mensal_preenche_meses_sem_gasto()
    {
        var series = SpendingCalculator.Monthly([Spend(7, 100m), Spend(9, 300m)], Months.Ending(M(9), 3));

        series.Should().Equal(new MonthTotal(M(7), 100m), new MonthTotal(M(8), 0m), new MonthTotal(M(9), 300m));
    }
}

public sealed class VariationAnalyzerTests
{
    [Fact]
    public void Compara_com_o_mes_anterior_e_separa_aumentos_e_reducoes()
    {
        SpendingEntry[] entries =
        [
            Spend(8, 620m, Restaurants), Spend(9, 980m, Restaurants),
            Spend(8, 500m, Market), Spend(9, 300m, Market),
            Spend(8, 100m, Leisure), Spend(9, 110m, Leisure),
        ];

        var comparisons = VariationAnalyzer.Compare(entries, M(9), [M(8)], Lookup, CategoryLevel.Leaf);

        var restaurants = comparisons.Single(c => c.Category.Id == Restaurants.Id);
        restaurants.Should().Be(new CategoryComparison(restaurants.Category, 980m, 620m, 360m, 360m / 620m));
        VariationAnalyzer.Increased(comparisons).Select(c => c.Category.Name).Should().Equal("Alimentação > Restaurantes");
        VariationAnalyzer.Decreased(comparisons).Select(c => c.Category.Name).Should().Equal("Supermercado");
    }

    [Fact]
    public void Compara_com_a_media_dos_meses_anteriores()
    {
        SpendingEntry[] entries = [Spend(6, 400m, Leisure), Spend(7, 520m, Leisure), Spend(8, 890m, Leisure), Spend(9, 750m, Leisure)];

        var comparison = VariationAnalyzer.Compare(entries, M(9), [M(6), M(7), M(8)], Lookup, CategoryLevel.Root).Single();

        comparison.Baseline.Should().Be(603.33m);
        comparison.Difference.Should().Be(146.67m);
    }

    [Fact]
    public void Variacoes_pequenas_nao_sao_destacadas_e_gasto_novo_e()
    {
        SpendingEntry[] entries = [Spend(8, 1000m, Market), Spend(9, 1050m, Market), Spend(9, 80m, Leisure), Spend(8, 20m, Food), Spend(9, 45m, Food)];

        var comparisons = VariationAnalyzer.Compare(entries, M(9), [M(8)], Lookup, CategoryLevel.Root);

        VariationAnalyzer.Increased(comparisons).Select(c => (c.Category.Name, c.Percent)).Should().Equal(("Lazer", (decimal?)null));
    }
}

public sealed class CommitmentProjectorTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Card = Guid.CreateVersion7();

    [Fact]
    public void Projeta_parcelas_ainda_nao_lancadas_nas_proximas_faturas()
    {
        var notebook = InstallmentPurchase.Create(Card, "Notebook", 500m, 12, M(8), Now);   // ago/26 a jul/27
        var tv = InstallmentPurchase.Create(Card, "TV", 300m, 3, M(9), Now);                // set/26 a nov/26
        var posted = new HashSet<(Guid, int)> { (notebook.Id, 3), (tv.Id, 2) };           // parcelas de outubro já importadas

        var projection = CommitmentProjector.Project([notebook, tv], Months.Ending(M(1, 2027), 4), posted);

        projection.Should().Equal(
            new MonthCommitment(M(10), 0m, 0),
            new MonthCommitment(M(11), 800m, 2),
            new MonthCommitment(M(12), 500m, 1),
            new MonthCommitment(M(1, 2027), 500m, 1));
    }

    [Fact]
    public void Valor_pendente_ignora_parcelas_antigas_nao_importadas()
    {
        var notebook = InstallmentPurchase.Create(Card, "Notebook", 500m, 12, M(8), Now);
        var posted = new HashSet<(Guid, int)> { (notebook.Id, 3) };

        CommitmentProjector.Outstanding([notebook], M(10), posted).Should().Be(4500m);
    }
}

public sealed class InsightGeneratorTests
{
    private static readonly CategoryKey Restaurants = new(Guid.CreateVersion7(), "Restaurantes", "#000000");
    private static readonly CategoryKey Food = new(Guid.CreateVersion7(), "Alimentação", "#000000");
    private static readonly CategoryKey Leisure = new(Guid.CreateVersion7(), "Lazer", "#000000");

    [Fact]
    public void Gera_insights_somente_a_partir_dos_dados()
    {
        var input = new InsightInput(
            HistoryMonths: 6,
            VersusAverage:
            [
                new(Restaurants, 660m, 500m, 160m, 0.32m),
                new(Food, 900m, 920m, -20m, -0.0217m),
                new(Leisure, 200m, 400m, -200m, -0.5m),
            ],
            VersusPreviousMonth: [new(Leisure, 200m, 100m, 100m, 1m)],
            OutstandingInstallments: 4200m,
            RecurringCount: 8,
            RecurringMonthly: 280m);

        InsightGenerator.Generate(input).Select(i => i.Message).Should().Equal(
            "Seu gasto com Restaurantes aumentou 32% em relação à média dos últimos 6 meses.",
            "O maior aumento de gastos neste mês ocorreu na categoria Lazer.",
            "Seu gasto com Lazer diminuiu 50% em relação à média dos últimos 6 meses.",
            "Você possui R$ 4.200,00 em compras parceladas ainda não finalizadas.",
            "Suas assinaturas e gastos recorrentes (8) representam aproximadamente R$ 280,00/mês.",
            "Seu gasto médio mensal com Alimentação nos últimos 6 meses foi de R$ 920,00.");
    }

    [Fact]
    public void Sem_historico_nao_compara_com_media()
    {
        var input = new InsightInput(1, [new(Restaurants, 660m, 500m, 160m, 0.32m)], [], 0m, 0, 0m);

        InsightGenerator.Generate(input).Should().BeEmpty();
    }
}
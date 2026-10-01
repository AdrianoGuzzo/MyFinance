using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class CategoryRuleTests
{
    private static readonly Category Delivery = Category.Create("Alimentação").CreateSubcategory("Delivery");
    private static readonly Category Transport = Category.Create("Transporte");

    [Fact]
    public void Create_normaliza_o_padrao()
    {
        var rule = CategoryRule.Create("  ifood ", Delivery, 0, Now);

        rule.Pattern.Should().Be("IFOOD");
        rule.CategoryId.Should().Be(Delivery.Id);
        rule.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("IFOOD *RESTAURANTE", true)]
    [InlineData("IFD*IFOOD SAO PAULO", true)]
    [InlineData("PAGAMENTO IFOODCOM", true)]
    [InlineData("SUPERIFOOD", false)]
    [InlineData("MERCADO", false)]
    public void Matches_exige_inicio_de_palavra(string description, bool expected)
    {
        CategoryRule.Create("IFOOD", Delivery, 0, Now).Matches(description).Should().Be(expected);
    }

    [Theory]
    [InlineData("I", 0)]
    [InlineData("  ", 0)]
    [InlineData("IFOOD", -1)]
    [InlineData("IFOOD", 1001)]
    public void Dados_invalidos_falham(string pattern, int priority)
    {
        var act = () => CategoryRule.Create(pattern, Delivery, priority, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Categoria_desativada_falha()
    {
        var inactive = Category.Create("Antiga");
        inactive.Deactivate();

        var act = () => CategoryRule.Create("LOJA", inactive, 0, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Matcher_aplica_a_regra_que_casa()
    {
        var rules = new[] { CategoryRule.Create("IFOOD", Delivery, 0, Now), CategoryRule.Create("UBER", Transport, 0, Now) };

        CategoryRuleMatcher.Match(rules, "Uber *Trip")!.CategoryId.Should().Be(Transport.Id);
    }

    [Fact]
    public void Matcher_sem_regra_correspondente_retorna_nulo()
    {
        var rules = new[] { CategoryRule.Create("IFOOD", Delivery, 0, Now) };

        CategoryRuleMatcher.Match(rules, "Padaria").Should().BeNull();
        CategoryRuleMatcher.Match(rules, "").Should().BeNull();
    }

    [Fact]
    public void Matcher_prefere_maior_prioridade_depois_padrao_mais_longo()
    {
        var generic = CategoryRule.Create("UBER", Transport, 0, Now);
        var specific = CategoryRule.Create("UBER EATS", Delivery, 0, Now);
        var priority = CategoryRule.Create("UBER", Delivery, 10, Now);

        CategoryRuleMatcher.Match([generic, specific], "UBER EATS PEDIDO").Should().Be(specific);
        CategoryRuleMatcher.Match([generic, specific, priority], "UBER EATS PEDIDO").Should().Be(priority);
    }

    [Fact]
    public void Matcher_ignora_regras_inativas()
    {
        var rule = CategoryRule.Create("IFOOD", Delivery, 0, Now);
        rule.Deactivate();

        CategoryRuleMatcher.Match([rule], "IFOOD").Should().BeNull();
    }
}

public sealed class SpendingLimitTests
{
    private static SpendingLimit Limit(decimal amount) => SpendingLimit.Create(Category.Create("Lazer"), amount, Now);

    [Fact]
    public void Dentro_do_limite()
    {
        var evaluation = Limit(800m).Evaluate(620m);

        evaluation.Should().Be(new LimitEvaluation(800m, 620m, Available: 180m, Excess: 0m, 0.775m, LimitStatus.Within));
    }

    [Theory]
    [InlineData(640)]
    [InlineData(799.99)]
    [InlineData(800)]
    public void Proximo_do_limite_a_partir_de_80_por_cento(decimal used)
    {
        Limit(800m).Evaluate(used).Status.Should().Be(LimitStatus.Near);
    }

    [Fact]
    public void Limite_excedido()
    {
        var evaluation = Limit(500m).Evaluate(620m);

        evaluation.Status.Should().Be(LimitStatus.Exceeded);
        evaluation.Excess.Should().Be(120m);
        evaluation.Available.Should().Be(0m);
    }

    [Fact]
    public void Limite_zero_ou_negativo_falha()
    {
        var act = () => Limit(0m);

        act.Should().Throw<DomainException>();
    }
}

public sealed class FinancialGoalAndRecurringTests
{
    [Fact]
    public void Meta_guarda_objetivo_mensal_e_anual()
    {
        var goal = FinancialGoal.Create("Economizar", 1000m, Day(15), Now);

        goal.MonthlyTarget.Should().Be(1000m);
        goal.AnnualTarget.Should().Be(12000m);
        goal.StartMonth.Should().Be(Day(1));
        ((Action)(() => goal.Update("Economizar", 0m, Day(1)))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Recorrente_preserva_classificacao_ao_atualizar_deteccao()
    {
        var netflix = RecurringExpense.Create("NETFLIX.COM", "Netflix.com", 55.90m, null, Day(1), Now);
        netflix.Classify(RecurringClassification.Optional, Now);

        netflix.Refresh("Netflix.com", 59.90m, null, Day(1, 10), Now);

        netflix.Classification.Should().Be(RecurringClassification.Optional);
        netflix.EstimatedMonthlyAmount.Should().Be(59.90m);
        netflix.EstimatedAnnualAmount.Should().Be(718.80m);
        netflix.LastSeenMonth.Should().Be(Day(1, 10));
    }

    [Fact]
    public void Recorrente_pode_ser_descartado_e_restaurado()
    {
        var gym = RecurringExpense.Create("ACADEMIA", "Academia", 99m, null, Day(1), Now);

        gym.Dismiss(Now);
        gym.IsDismissed.Should().BeTrue();

        gym.Restore(Now);
        gym.IsDismissed.Should().BeFalse();
    }
}
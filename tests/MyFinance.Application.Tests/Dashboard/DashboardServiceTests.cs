using MyFinance.Application.Dashboard;

namespace MyFinance.Application.Tests.Dashboard;

public sealed class DashboardServiceTests : ApplicationTestBase
{
    [Fact]
    public async Task Dashboard_vazio()
    {
        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.ReferenceMonth.Should().Be(Month(9));
        dashboard.MonthSpending.Should().Be(0);
        dashboard.SpendingByCategory.Should().BeEmpty();
        dashboard.MonthlySpending.Should().HaveCount(6);
    }

    [Fact]
    public async Task Dashboard_soma_gastos_pela_competencia_da_fatura()
    {
        // Hoje: 30/09/2026; fechamento dia 3 → fatura aberta é a de outubro.
        var card = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await SpendAsync(card, Month(9), 400m, "Mercado agosto", "Supermercado");
        await SpendAsync(card, Month(10), 300m, "Mercado", "Supermercado");
        await SpendAsync(card, Month(10), 89m, "iFood", "Alimentação > Delivery");
        await SpendAsync(card, Month(10), 50m, "Sem categoria");
        await AddTransactionAsync(card, Day(20), 1000m, "Pagamento recebido");

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.ReferenceMonth.Should().Be(Month(10));
        dashboard.MonthSpending.Should().Be(439m, "o pagamento da fatura não é gasto");
        dashboard.CurrentInvoiceTotal.Should().Be(439m);
        dashboard.SpendingByCategory.Select(c => (c.CategoryName, c.Amount)).Should().Equal(
            ("Supermercado", 300m), ("Alimentação", 89m), ("Sem categoria", 50m));
        dashboard.MonthlySpending.Select(m => (m.Month, m.Amount)).Should().EndWith([(Month(9), 400m), (Month(10), 439m)]);
    }
}
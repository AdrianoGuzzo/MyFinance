using MyFinance.Application.Dashboard;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests.Dashboard;

public sealed class DashboardServiceTests : ApplicationTestBase
{
    [Fact]
    public async Task Dashboard_vazio()
    {
        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.TotalBalance.Should().Be(0);
        dashboard.MonthBalance.Should().Be(0);
        dashboard.ExpensesByCategory.Should().BeEmpty();
        dashboard.IncomeVsExpenses.Should().HaveCount(6);
        dashboard.BalanceEvolution.Should().HaveCount(6);
    }

    [Fact]
    public async Task Dashboard_calcula_saldo_mes_fatura_e_graficos()
    {
        // Hoje: 30/09/2026.
        var account = TransactionOwner.ForAccount(await CreateAccountAsync(initialBalance: 1000m));
        var card = TransactionOwner.ForCreditCard(await CreateCardAsync(closingDay: 3, dueDay: 10));

        await AddTransactionAsync(account, Day(5, 8), 4000m, "Salário agosto", "Salário");
        await AddTransactionAsync(account, Day(10, 8), -200m, "Farmácia");
        await AddTransactionAsync(account, Day(5), 5000m, "Salário setembro", "Salário");
        await AddTransactionAsync(account, Day(10), -1500m, "Aluguel", "Moradia");
        await AddTransactionAsync(account, Day(15), -300m, "Mercado", "Alimentação > Mercado");
        await AddTransactionAsync(account, Day(20), -800m, "Pagamento da fatura", "Transferências > Pagamento de fatura");
        await AddTransactionAsync(card, Day(1), -100m, "iFood", "Alimentação > Delivery");
        await AddTransactionAsync(card, Day(4), -50m, "Uber");
        await AddTransactionAsync(card, Day(25), -49.90m, "Netflix", "Assinaturas");
        await AddTransactionAsync(card, Day(20), 800m, "Pagamento recebido", "Transferências > Pagamento de fatura");

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.ReferenceMonth.Should().Be(Day(1));
        dashboard.TotalBalance.Should().Be(7200m, "1000 + 4000 - 200 + 5000 - 1500 - 300 - 800");
        dashboard.MonthIncome.Should().Be(5000m);
        dashboard.MonthExpenses.Should().Be(1999.90m, "aluguel, mercado e compras no cartão; o pagamento da fatura não conta");
        dashboard.MonthBalance.Should().Be(3000.10m);

        dashboard.CurrentInvoiceTotal.Should().Be(99.90m, "compras de 03/09 a 02/10");
        dashboard.Invoices.Single().Invoice.DueDate.Should().Be(Day(10, 10));

        dashboard.ExpensesByCategory.Select(c => (c.CategoryName, c.Amount)).Should().Equal(
            ("Moradia", 1500m),
            ("Alimentação", 400m),
            ("Sem categoria", 50m),
            ("Assinaturas", 49.90m));

        dashboard.IncomeVsExpenses.Select(m => (m.Month, m.Income, m.Expenses)).Should().Equal(
            (Day(1, 4), 0m, 0m),
            (Day(1, 5), 0m, 0m),
            (Day(1, 6), 0m, 0m),
            (Day(1, 7), 0m, 0m),
            (Day(1, 8), 4000m, 200m),
            (Day(1, 9), 5000m, 1999.90m));

        dashboard.BalanceEvolution.Select(p => (p.Date, p.Balance)).Should().Equal(
            (Day(30, 4), 1000m),
            (Day(31, 5), 1000m),
            (Day(30, 6), 1000m),
            (Day(31, 7), 1000m),
            (Day(31, 8), 4800m),
            (Day(30, 9), 7200m));
    }

    [Fact]
    public async Task Evolucao_do_saldo_considera_lancamentos_anteriores_ao_historico()
    {
        var account = TransactionOwner.ForAccount(await CreateAccountAsync(initialBalance: 100m));
        await AddTransactionAsync(account, Day(15, 1), 900m, "Janeiro");

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.BalanceEvolution.Should().AllSatisfy(p => p.Balance.Should().Be(1000m));
    }
}
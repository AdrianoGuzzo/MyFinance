using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Dashboard;
using MyFinance.Application.Imports;
using MyFinance.Application.Invoices;
using MyFinance.Application.Tests.Imports;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests.EdgeCases;

/// <summary>Fluxo completo com as faturas fictícias de <c>TestFiles</c> (as mesmas dos testes dos importadores).</summary>
public sealed class EndToEndImportTests : ApplicationTestBase
{
    private static FileStream Fixture(string name) => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestFiles", name));

    [Fact]
    public async Task OFX_de_cartao_e_importado_categorizado_e_reimportado_sem_duplicar()
    {
        var cardId = await CreateCardAsync(lastFour: "1234", closingDay: 3, dueDay: 10);
        await Host.Get<CategoryService>().EnsureDefaultCategoriesAsync(Ct);
        var service = Host.Get<ImportService>();

        await using (var file = Fixture("cartao-sgml.ofx"))
        {
            var analysis = await service.AnalyzeAsync("cartao-sgml.ofx", file, Ct);
            analysis.SuggestedCreditCardId.Should().Be(cardId);
            var preview = await service.PreviewAsync(analysis, cardId, null, Ct);
            preview.Rows.Select(r => (r.Kind, r.InvoiceMonth)).Should().Equal(
                (TransactionKind.Purchase, Month(10)), (TransactionKind.Purchase, Month(10)), (TransactionKind.Payment, Month(10)));
            (await service.ConfirmAsync(preview, Ct)).Summary.Should().Be(new ImportSummary(3, 3, 0, 0));
        }

        await using (var again = Fixture("cartao-sgml.ofx"))
        {
            var analysis = await service.AnalyzeAsync("cartao-sgml.ofx", again, Ct);
            analysis.PreviousImport.Should().NotBeNull();
            (await service.ConfirmAsync(await service.PreviewAsync(analysis, cardId, null, Ct), Ct)).Summary.New.Should().Be(0);
        }

        var invoice = (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Should().ContainSingle().Subject;
        invoice.Total.Should().Be(138.90m);
        invoice.Payments.Should().Be(1500m);

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);
        dashboard.MonthSpending.Should().Be(138.90m);
        dashboard.Categories.Select(c => (c.Name, c.Amount)).Should().Equal(("Alimentação", 89m), ("Assinaturas", 49.90m));
    }

    [Fact]
    public async Task CSV_de_cartao_com_compras_positivas_e_importado_com_inversao_sugerida()
    {
        var cardId = await CreateCardAsync(closingDay: 3, dueDay: 10);
        var service = Host.Get<ImportService>();

        await using var file = Fixture("nubank-cartao.csv");
        var analysis = await service.AnalyzeAsync("nubank-cartao.csv", file, Ct);
        var preview = await service.PreviewAsync(analysis, cardId, null, Ct);
        await service.ConfirmAsync(preview, Ct);

        preview.AmountsInverted.Should().BeTrue();
        (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Single().Total.Should().Be(138.90m);
    }
}

public sealed class EdgeCaseTests : ApplicationTestBase
{
    [Fact]
    public async Task Estorno_categorizado_pela_regra_reduz_o_gasto_da_categoria()
    {
        var cardId = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await Host.Get<CategoryService>().EnsureDefaultCategoriesAsync(Ct);
        var service = Host.Get<ImportService>();
        var ofx = ImportServiceTests.Ofx("1234", ("20260910", "-89.00", "1", "IFOOD *PIZZARIA"), ("20260912", "30.00", "2", "ESTORNO IFOOD *PIZZARIA"));
        await service.ConfirmAsync(await service.PreviewAsync(await service.AnalyzeAsync("f.ofx", Text(ofx), Ct), cardId, null, Ct), Ct);

        var refund = (await Host.Get<TransactionService>().SearchAsync(new TransactionSearch { Text = "ESTORNO" }, Ct)).Items.Single();
        refund.Kind.Should().Be(TransactionKind.Refund);
        refund.CategoryName.Should().Be("Alimentação > Delivery");

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);
        dashboard.Categories.Single().Should().Match<CategorySpendingDto>(c => c.Name == "Alimentação" && c.Amount == 59m);
    }

    [Fact]
    public async Task Corrigir_o_tipo_atualiza_o_total_da_fatura()
    {
        var cardId = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await AddTransactionAsync(cardId, Day(10), -500m, "Mercado");
        var credit = await AddTransactionAsync(cardId, Day(11), 500m, "Credito");

        (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Single().Total.Should().Be(0m, "classificado como estorno");

        await Host.Get<TransactionService>().ChangeKindAsync(credit, TransactionKind.Payment, Ct);

        (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Single().Total.Should().Be(500m, "pagamento não reduz a fatura");
    }

    [Fact]
    public async Task Cartoes_com_fechamentos_diferentes_e_cartao_inativo()
    {
        // Hoje 30/09: cartão A (fecha 3) tem a fatura de outubro aberta; cartão B (fecha 25, vence 5) já está na de novembro.
        var a = await CreateCardAsync("A", "1111", closingDay: 3, dueDay: 10);
        var b = await CreateCardAsync("B", "2222", closingDay: 25, dueDay: 5);
        var c = await CreateCardAsync("C", "3333", closingDay: 3, dueDay: 10);
        await AddTransactionAsync(a, Day(20), -100m, "Compra A");
        await AddTransactionAsync(b, Day(20), -200m, "Compra B fatura de outubro");
        await AddTransactionAsync(b, Day(27), -300m, "Compra B fatura de novembro");
        await AddTransactionAsync(c, Day(20), -400m, "Compra C");
        await Host.Get<CreditCardService>().DeactivateAsync(c, Ct);

        var dashboard = await Host.Get<DashboardService>().GetAsync(Ct);

        dashboard.ReferenceMonth.Should().Be(Month(10), "a fatura atual é a aberta mais próxima do vencimento");
        dashboard.IsPartial.Should().BeTrue();
        dashboard.MonthSpending.Should().Be(700m, "o mês soma as faturas de outubro de todos os cartões, inclusive inativos");
        dashboard.OpenInvoices.Select(i => (i.CreditCardName, i.Invoice.ReferenceMonth, i.Invoice.Amount)).Should().Equal(
            ("A", Month(10), 100m), ("B", Month(11), 300m));
        dashboard.CurrentInvoiceTotal.Should().Be(400m);
    }

    [Fact]
    public async Task Cartao_sem_lancamentos_tem_fatura_aberta_zerada()
    {
        await CreateCardAsync();

        var card = (await Host.Get<CreditCardService>().ListAsync(false, Ct)).Single();

        card.CurrentInvoice.Amount.Should().Be(0m);
        card.LimitUsage.Should().Be(0m);
        (await Host.Get<InvoiceService>().ListAsync(card.Id, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Mes_sem_dados_e_anterior_ao_historico()
    {
        var cardId = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await SpendAsync(cardId, Month(9), 100m, "Mercado");

        var empty = await Host.Get<DashboardService>().GetAsync(Month(1), 6, Ct);

        empty.MonthSpending.Should().Be(0m);
        empty.Average.Should().BeNull();
        empty.HistoryMonths.Should().Be(0);
        empty.Categories.Should().BeEmpty();
        empty.Insights.Should().BeEmpty();
    }

    [Fact]
    public async Task Fechamento_dia_31_em_fevereiro()
    {
        var cardId = await CreateCardAsync(closingDay: 31, dueDay: 8);
        await AddTransactionAsync(cardId, Day(27, 2), -50m, "Antes do fechamento (28/02)");
        await AddTransactionAsync(cardId, Day(28, 2), -70m, "No dia do fechamento");

        var invoices = await Host.Get<InvoiceService>().ListAsync(cardId, Ct);

        invoices.Select(i => (i.ReferenceMonth, i.ClosingDate, i.Total)).Should().Equal(
            (Month(4), Day(31, 3), 70m),
            (Month(3), Day(28, 2), 50m));
    }
}
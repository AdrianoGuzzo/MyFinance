using MyFinance.Domain.Entities;
using MyFinance.Domain.Services;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Services;

public sealed class InvoiceAssignerTests
{
    // Card: fecha dia 3, vence dia 10. Compras de 03/09 a 02/10 → fatura de outubro.
    private static InvoiceAssignmentItem Item(DateOnly date, string description) => new(date, InstallmentParser.Parse(description));

    [Fact]
    public void Sem_parcelas_usa_a_fatura_pela_data()
    {
        var months = InvoiceAssigner.Assign(Card, [Item(Day(2), "Padaria"), Item(Day(3), "Mercado"), Item(Day(1, 10), "Uber")], null);

        months.Should().Equal(Day(1, 9), Day(1, 10), Day(1, 10));
    }

    [Fact]
    public void Parcela_com_a_data_original_da_compra_vai_para_a_fatura_do_arquivo()
    {
        // Compra de julho em 12x: a 3ª parcela cai na fatura de outubro, como as demais linhas do arquivo.
        var months = InvoiceAssigner.Assign(Card,
        [
            Item(Day(10), "Mercado"),
            Item(Day(20), "Restaurante"),
            Item(Day(10, 7), "Notebook - Parcela 3/12"),
        ], null);

        months.Should().Equal(Day(1, 10), Day(1, 10), Day(1, 10));
    }

    [Fact]
    public void Parcela_com_a_data_de_lancamento_fica_na_fatura_pela_data()
    {
        var months = InvoiceAssigner.Assign(Card,
        [
            Item(Day(10), "Mercado"),
            Item(Day(12), "Notebook - Parcela 3/12"),
        ], null);

        months.Should().Equal(Day(1, 10), Day(1, 10));
    }

    [Fact]
    public void Arquivo_so_com_parcelas_usa_a_fatura_pela_data()
    {
        var months = InvoiceAssigner.Assign(Card, [Item(Day(10, 7), "Notebook - Parcela 3/12")], null);

        months.Should().Equal(Day(1, 8));
    }

    [Fact]
    public void Fatura_escolhida_pelo_usuario_vale_para_todo_o_arquivo()
    {
        var months = InvoiceAssigner.Assign(Card, [Item(Day(2), "Padaria"), Item(Day(10, 7), "Notebook - Parcela 3/12")], Day(15, 11));

        months.Should().Equal(Day(1, 11), Day(1, 11));
    }
}

public sealed class InstallmentMatcherTests
{
    private static readonly DateOnly October = Day(1, 10);

    private static InstallmentPurchase Notebook(decimal amount = 500m, int count = 12, DateOnly? first = null) =>
        InstallmentPurchase.Create(Card.Id, "Notebook", amount, count, first ?? Day(1, 8), Now);

    [Fact]
    public void Parcela_seguinte_e_vinculada_a_mesma_compra()
    {
        var notebook = Notebook();

        var match = InstallmentMatcher.FindMatch([notebook], "NOTEBOOK", new InstallmentInfo("Notebook", 4, 12), Day(1, 11), 500m, (_, _) => false);

        match.Should().Be(notebook);
    }

    [Fact]
    public void Ultima_parcela_com_centavos_de_diferenca_tambem_e_vinculada()
    {
        var notebook = Notebook(amount: 333.33m, count: 3);

        var match = InstallmentMatcher.FindMatch([notebook], "NOTEBOOK", new InstallmentInfo("Notebook", 3, 3), October, 333.34m, (_, _) => false);

        match.Should().Be(notebook);
    }

    [Theory]
    [InlineData("OUTRA LOJA", 3, 12, 10, 500)]   // outro estabelecimento
    [InlineData("NOTEBOOK", 3, 10, 10, 500)]     // outra quantidade de parcelas
    [InlineData("NOTEBOOK", 3, 12, 11, 500)]     // compra de outro mês
    [InlineData("NOTEBOOK", 3, 12, 10, 450)]     // outro valor
    public void Compra_diferente_nao_e_vinculada(string merchantKey, int number, int count, int month, decimal amount)
    {
        var match = InstallmentMatcher.FindMatch([Notebook()], merchantKey, new InstallmentInfo("x", number, count), Day(1, month), amount, (_, _) => false);

        match.Should().BeNull();
    }

    [Fact]
    public void Parcela_ja_vinculada_indica_outra_compra_identica()
    {
        var first = Notebook();
        var second = Notebook();

        var match = InstallmentMatcher.FindMatch([first, second], "NOTEBOOK", new InstallmentInfo("Notebook", 3, 12), October, 500m,
            (purchase, number) => purchase == first.Id && number == 3);

        match.Should().Be(second);
    }
}
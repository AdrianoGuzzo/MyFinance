using MyFinance.Domain.Entities;
using MyFinance.Domain.Exceptions;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class InstallmentPurchaseTests
{
    /// <summary>Notebook de R$ 6.000 em 12x de R$ 500, 1ª parcela na fatura de outubro/2026.</summary>
    private static InstallmentPurchase Notebook() =>
        InstallmentPurchase.Create(Card.Id, "Notebook - Parcela 1/12", 500m, 12, Day(15, 10), Now);

    [Fact]
    public void Doze_parcelas_diferenciam_valor_total_e_impacto_mensal()
    {
        var notebook = Notebook();

        notebook.Description.Should().Be("Notebook");
        notebook.MerchantKey.Should().Be("NOTEBOOK");
        notebook.TotalAmount.Should().Be(6000m);
        notebook.InstallmentAmount.Should().Be(500m);
        notebook.FirstInvoiceMonth.Should().Be(Day(1, 10));
        notebook.LastInvoiceMonth.Should().Be(Day(1, 9, 2027));
    }

    [Fact]
    public void Cronograma_tem_uma_parcela_por_fatura()
    {
        var schedule = Notebook().Schedule();

        schedule.Should().HaveCount(12);
        schedule[0].Should().Be(new InstallmentSlot(1, Day(1, 10), 500m));
        schedule[11].Should().Be(new InstallmentSlot(12, Day(1, 9, 2027), 500m));
        schedule.Sum(s => s.Amount).Should().Be(6000m);
    }

    [Theory]
    [InlineData(9, 2026, 12, 6000)]    // antes da 1ª parcela: tudo a pagar
    [InlineData(10, 2026, 11, 5500)]   // fatura da 1ª parcela: restam 11
    [InlineData(3, 2027, 6, 3000)]     // fatura da 6ª parcela: restam 6 (R$ 3.000)
    [InlineData(9, 2027, 0, 0)]        // fatura da última parcela
    [InlineData(1, 2028, 0, 0)]        // depois do fim
    public void Parcelas_e_valor_restantes_apos_um_mes(int month, int year, int remaining, decimal amount)
    {
        var notebook = Notebook();

        notebook.RemainingCount(Day(1, month, year)).Should().Be(remaining);
        notebook.RemainingAmount(Day(1, month, year)).Should().Be(amount);
    }

    [Fact]
    public void NumberIn_indica_a_parcela_de_cada_fatura()
    {
        var notebook = Notebook();

        notebook.NumberIn(Day(1, 9)).Should().BeNull();
        notebook.NumberIn(Day(20, 10)).Should().Be(1);
        notebook.NumberIn(Day(1, 3, 2027)).Should().Be(6);
        notebook.NumberIn(Day(1, 10, 2027)).Should().BeNull();
        notebook.InvoiceMonthOf(6).Should().Be(Day(1, 3, 2027));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(-10, 12)]
    [InlineData(100, 1)]
    [InlineData(100, 49)]
    public void Dados_invalidos_falham(decimal amount, int count)
    {
        var act = () => InstallmentPurchase.Create(Card.Id, "Compra", amount, count, Day(1, 10), Now);

        act.Should().Throw<DomainException>();
    }
}
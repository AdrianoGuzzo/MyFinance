using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class CreditCardTests
{
    [Fact]
    public void Create_preenche_instituicao_e_bandeira()
    {
        var card = NewCard(closingDay: 3, dueDay: 10);

        card.Issuer.Should().Be("Nu Pagamentos");
        card.Brand.Should().Be(CardBrand.Mastercard);
        card.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_com_limite_negativo_falha()
    {
        var act = () => CreditCard.Create("Cartão", "Banco", CardBrand.Visa, LastFourDigits.Create("1234"), -1m,
            DayOfMonth.Create(1), DayOfMonth.Create(10), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_com_bandeira_invalida_falha()
    {
        var act = () => CreditCard.Create("Cartão", "Banco", (CardBrand)42, LastFourDigits.Create("1234"), 0m,
            DayOfMonth.Create(1), DayOfMonth.Create(10), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Compra_antes_do_fechamento_entra_na_fatura_do_mes()
    {
        var period = NewCard(closingDay: 3, dueDay: 10).GetInvoicePeriod(Day(2));

        period.Should().Be(new InvoicePeriod(Day(3, 8), Day(3), Day(10)));
        period.ReferenceMonth.Should().Be(Day(1));
    }

    [Fact]
    public void Compra_no_dia_do_fechamento_entra_na_fatura_seguinte()
    {
        var period = NewCard(closingDay: 3, dueDay: 10).GetInvoicePeriod(Day(3));

        period.Should().Be(new InvoicePeriod(Day(3), Day(3, 10), Day(10, 10)));
    }

    [Fact]
    public void Vencimento_anterior_ao_fechamento_cai_no_mes_seguinte()
    {
        var period = NewCard(closingDay: 25, dueDay: 5).GetInvoicePeriod(Day(30));

        period.Should().Be(new InvoicePeriod(Day(25), Day(25, 10), Day(5, 11)));
    }

    [Fact]
    public void Fechamento_dia_31_e_ajustado_em_meses_curtos()
    {
        var card = NewCard(closingDay: 31, dueDay: 8);

        card.GetInvoicePeriod(Day(15, 2)).Should().Be(new InvoicePeriod(Day(31, 1), Day(28, 2), Day(8, 3)));
        card.GetInvoicePeriod(Day(28, 2)).Should().Be(new InvoicePeriod(Day(28, 2), Day(31, 3), Day(8, 4)));
    }

    [Fact]
    public void Periodo_contem_inicio_e_nao_contem_fechamento()
    {
        var period = NewCard(closingDay: 3, dueDay: 10).GetInvoicePeriod(Day(2));

        period.Contains(Day(3, 8)).Should().BeTrue();
        period.Contains(Day(2)).Should().BeTrue();
        period.Contains(Day(3)).Should().BeFalse();
    }

    [Theory]
    [InlineData(3, 10)]
    [InlineData(25, 5)]
    [InlineData(31, 8)]
    [InlineData(10, 10)]
    public void Periodo_pelo_mes_de_referencia_e_o_mesmo_calculado_pela_data(int closingDay, int dueDay)
    {
        var card = NewCard(closingDay, dueDay);

        for (var date = Day(1, 1); date < Day(1, 1, 2028); date = date.AddDays(1))
        {
            var byDate = card.GetInvoicePeriod(date);
            card.GetInvoicePeriodForMonth(byDate.ReferenceMonth).Should().Be(byDate);
        }
    }
}

public sealed class InvoiceTests
{
    [Fact]
    public void For_copia_o_periodo_e_o_mes_de_referencia()
    {
        October.CreditCardId.Should().Be(Card.Id);
        October.ReferenceMonth.Should().Be(Day(1, 10));
        October.StartDate.Should().Be(Day(3, 9));
        October.ClosingDate.Should().Be(Day(3, 10));
        October.DueDate.Should().Be(Day(10, 10));
    }

    [Theory]
    [InlineData(20, 9, InvoiceStatus.Open)]
    [InlineData(2, 10, InvoiceStatus.Open)]
    [InlineData(3, 10, InvoiceStatus.Closed)]
    [InlineData(10, 10, InvoiceStatus.Closed)]
    [InlineData(11, 10, InvoiceStatus.Overdue)]
    public void Status_e_derivado_das_datas(int day, int month, InvoiceStatus expected)
    {
        var invoice = Invoice.For(Card, Card.GetInvoicePeriodForMonth(Day(1, 10)));

        invoice.GetStatus(Day(day, month)).Should().Be(expected);
    }

    [Fact]
    public void Fatura_paga_fica_paga_mesmo_apos_o_vencimento()
    {
        var invoice = Invoice.For(Card, Card.GetInvoicePeriodForMonth(Day(1, 10)));

        invoice.MarkPaid(Now);

        invoice.GetStatus(Day(20, 11)).Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(Now);

        invoice.MarkUnpaid();
        invoice.GetStatus(Day(20, 11)).Should().Be(InvoiceStatus.Overdue);
    }
}
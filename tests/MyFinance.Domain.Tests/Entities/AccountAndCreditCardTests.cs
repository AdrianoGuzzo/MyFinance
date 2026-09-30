using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class AccountTests
{
    [Fact]
    public void Create_conta_nubank()
    {
        var account = Account.Create("Nubank", "Nu Pagamentos", AccountType.Payment, 1500m, Now,
            AccountNumber.Create("12345678-9"), "0001");

        account.Name.Should().Be("Nubank");
        account.AccountNumber!.Value.Should().Be("12345678-9");
        account.Agency.Should().Be("0001");
        account.IsActive.Should().BeTrue();
        account.BalanceWith(-200m).Should().Be(1300m);
    }

    [Fact]
    public void Create_com_tipo_invalido_falha()
    {
        var act = () => Account.Create("Conta", "Banco", (AccountType)99, 0m, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deactivate_desativa_conta()
    {
        var account = Account.Create("Conta", "Banco", AccountType.Checking, 0m, Now);

        account.Deactivate();

        account.IsActive.Should().BeFalse();
    }
}

public sealed class CreditCardTests
{
    private static CreditCard Card(int closingDay, int dueDay) => CreditCard.Create(
        "Nubank", "Nu Pagamentos", LastFourDigits.Create("1234"), 5000m,
        DayOfMonth.Create(closingDay), DayOfMonth.Create(dueDay), Now);

    [Fact]
    public void Create_com_limite_negativo_falha()
    {
        var act = () => CreditCard.Create("Cartão", "Banco", LastFourDigits.Create("1234"), -1m,
            DayOfMonth.Create(1), DayOfMonth.Create(10), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Compra_antes_do_fechamento_entra_na_fatura_do_mes()
    {
        var period = Card(closingDay: 3, dueDay: 10).GetInvoicePeriod(Day(2));

        period.Should().Be(new InvoicePeriod(Day(3, 8), Day(3), Day(10)));
        period.ReferenceMonth.Should().Be(Day(1));
    }

    [Fact]
    public void Compra_no_dia_do_fechamento_entra_na_fatura_seguinte()
    {
        var period = Card(closingDay: 3, dueDay: 10).GetInvoicePeriod(Day(3));

        period.Should().Be(new InvoicePeriod(Day(3), Day(3, 10), Day(10, 10)));
    }

    [Fact]
    public void Vencimento_anterior_ao_fechamento_cai_no_mes_seguinte()
    {
        var period = Card(closingDay: 25, dueDay: 5).GetInvoicePeriod(Day(30));

        period.Should().Be(new InvoicePeriod(Day(25), Day(25, 10), Day(5, 11)));
    }

    [Fact]
    public void Fechamento_dia_31_e_ajustado_em_meses_curtos()
    {
        var card = Card(closingDay: 31, dueDay: 8);

        card.GetInvoicePeriod(Day(15, 2)).Should().Be(new InvoicePeriod(Day(31, 1), Day(28, 2), Day(8, 3)));
        card.GetInvoicePeriod(Day(28, 2)).Should().Be(new InvoicePeriod(Day(28, 2), Day(31, 3), Day(8, 4)));
    }

    [Fact]
    public void Periodo_contem_inicio_e_nao_contem_fechamento()
    {
        var period = Card(closingDay: 3, dueDay: 10).GetInvoicePeriod(Day(2));

        period.Contains(Day(3, 8)).Should().BeTrue();
        period.Contains(Day(2)).Should().BeTrue();
        period.Contains(Day(3)).Should().BeFalse();
    }
}
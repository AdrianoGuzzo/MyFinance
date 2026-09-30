using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class TransactionTests
{
    [Fact]
    public void Create_com_dados_validos_preenche_propriedades()
    {
        var transaction = Transaction.Create(AccountOwner, Day(29), -120.50m, "  Supermercado  ", Now, externalId: "FIT123");

        transaction.Id.Should().NotBeEmpty();
        transaction.AccountId.Should().Be(AccountOwner.Id);
        transaction.CreditCardId.Should().BeNull();
        transaction.Date.Should().Be(Day(29));
        transaction.Amount.Should().Be(-120.50m);
        transaction.Description.Should().Be("Supermercado");
        transaction.ExternalId.Should().Be("FIT123");
        transaction.CreatedAt.Should().Be(Now);
        transaction.UpdatedAt.Should().BeNull();
        transaction.Owner.Should().Be(AccountOwner);
    }

    [Theory]
    [InlineData(10.00, TransactionType.Income)]
    [InlineData(-10.00, TransactionType.Expense)]
    public void Create_define_tipo_pelo_sinal_do_valor(decimal amount, TransactionType expected)
    {
        var transaction = Transaction.Create(AccountOwner, Day(1), amount, "Teste", Now);

        transaction.TransactionType.Should().Be(expected);
        transaction.IsIncome.Should().Be(expected == TransactionType.Income);
        transaction.IsExpense.Should().Be(expected == TransactionType.Expense);
    }

    [Fact]
    public void Create_com_valor_zero_falha()
    {
        var act = () => Transaction.Create(AccountOwner, Day(1), 0m, "Teste", Now);

        act.Should().Throw<DomainException>().WithMessage("*não pode ser zero*");
    }

    [Fact]
    public void Create_com_mais_de_duas_casas_decimais_falha()
    {
        var act = () => Transaction.Create(AccountOwner, Day(1), 10.123m, "Teste", Now);

        act.Should().Throw<DomainException>().WithMessage("*2 casas decimais*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_sem_descricao_falha(string? description)
    {
        var act = () => Transaction.Create(AccountOwner, Day(1), 10m, description!, Now);

        act.Should().Throw<DomainException>().WithMessage("*descrição*obrigatório*");
    }

    [Fact]
    public void Create_sem_conta_ou_cartao_falha()
    {
        var act = () => Transaction.Create(default, Day(1), 10m, "Teste", Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_com_data_de_auditoria_nao_utc_falha()
    {
        var act = () => Transaction.Create(AccountOwner, Day(1), 10m, "Teste", DateTime.Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_para_cartao_preenche_somente_CreditCardId()
    {
        var card = TransactionOwner.ForCreditCard(Guid.CreateVersion7());

        var transaction = Transaction.Create(card, Day(1), -50m, "Uber", Now);

        transaction.CreditCardId.Should().Be(card.Id);
        transaction.AccountId.Should().BeNull();
    }

    [Fact]
    public void Categorize_com_categoria_ativa_atribui_categoria_e_atualiza_data()
    {
        var transaction = Transaction.Create(AccountOwner, Day(1), -35.90m, "Uber", Now);
        var category = Category.Create("Transporte", CategoryType.Expense);
        var later = Now.AddMinutes(5);

        transaction.Categorize(category, later);

        transaction.CategoryId.Should().Be(category.Id);
        transaction.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Categorize_com_categoria_desativada_falha()
    {
        var transaction = Transaction.Create(AccountOwner, Day(1), -35.90m, "Uber", Now);
        var category = Category.Create("Transporte", CategoryType.Expense);
        category.Deactivate();

        var act = () => transaction.Categorize(category, Now);

        act.Should().Throw<DomainException>().WithMessage("*desativada*");
    }

    [Fact]
    public void RemoveCategory_limpa_categoria()
    {
        var transaction = Transaction.Create(AccountOwner, Day(1), -35.90m, "Uber", Now);
        transaction.Categorize(Category.Create("Transporte", CategoryType.Expense), Now);

        transaction.RemoveCategory(Now);

        transaction.CategoryId.Should().BeNull();
    }
}
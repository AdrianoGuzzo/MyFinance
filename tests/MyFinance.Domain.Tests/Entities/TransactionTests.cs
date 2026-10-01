using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class TransactionTests
{
    [Fact]
    public void Create_com_dados_validos_preenche_propriedades()
    {
        var transaction = Transaction.Create(October, Day(29), -120.50m, "  Supermercado  ", TransactionKind.Purchase, Now, externalId: "FIT123");

        transaction.Id.Should().NotBeEmpty();
        transaction.CreditCardId.Should().Be(Card.Id);
        transaction.InvoiceId.Should().Be(October.Id);
        transaction.Date.Should().Be(Day(29));
        transaction.Amount.Should().Be(-120.50m);
        transaction.Description.Should().Be("Supermercado");
        transaction.MerchantName.Should().Be("Supermercado");
        transaction.MerchantKey.Should().Be("SUPERMERCADO");
        transaction.Kind.Should().Be(TransactionKind.Purchase);
        transaction.ExternalId.Should().Be("FIT123");
        transaction.CreatedAt.Should().Be(Now);
        transaction.UpdatedAt.Should().BeNull();
        transaction.InstallmentPurchaseId.Should().BeNull();
    }

    [Theory]
    [InlineData(-100.00, TransactionKind.Purchase, 100.00)]
    [InlineData(-8.50, TransactionKind.Fee, 8.50)]
    [InlineData(-12.00, TransactionKind.Interest, 12.00)]
    [InlineData(30.00, TransactionKind.Refund, -30.00)]
    [InlineData(1500.00, TransactionKind.Payment, 0)]
    [InlineData(-5.00, TransactionKind.Adjustment, 5.00)]
    [InlineData(5.00, TransactionKind.Adjustment, -5.00)]
    public void SpendingAmount_conta_compras_desconta_estornos_e_ignora_pagamentos(decimal amount, TransactionKind kind, decimal expected)
    {
        var transaction = Transaction.Create(October, Day(1), amount, "Teste", kind, Now);

        transaction.SpendingAmount.Should().Be(expected);
    }

    [Theory]
    [InlineData(10.00, TransactionKind.Purchase)]
    [InlineData(10.00, TransactionKind.Fee)]
    [InlineData(10.00, TransactionKind.Interest)]
    [InlineData(-10.00, TransactionKind.Refund)]
    [InlineData(-10.00, TransactionKind.Payment)]
    public void Tipo_incompativel_com_o_sinal_falha(decimal amount, TransactionKind kind)
    {
        var act = () => Transaction.Create(October, Day(1), amount, "Teste", kind, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_com_valor_zero_falha()
    {
        var act = () => Transaction.Create(October, Day(1), 0m, "Teste", TransactionKind.Adjustment, Now);

        act.Should().Throw<DomainException>().WithMessage("*não pode ser zero*");
    }

    [Fact]
    public void Create_com_mais_de_duas_casas_decimais_falha()
    {
        var act = () => Transaction.Create(October, Day(1), -10.123m, "Teste", TransactionKind.Purchase, Now);

        act.Should().Throw<DomainException>().WithMessage("*2 casas decimais*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_sem_descricao_falha(string? description)
    {
        var act = () => Transaction.Create(October, Day(1), -10m, description!, TransactionKind.Purchase, Now);

        act.Should().Throw<DomainException>().WithMessage("*descrição*obrigatório*");
    }

    [Fact]
    public void Create_com_data_de_auditoria_nao_utc_falha()
    {
        var act = () => Transaction.Create(October, Day(1), -10m, "Teste", TransactionKind.Purchase, DateTime.Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estabelecimento_ignora_sufixo_de_parcela()
    {
        var transaction = Transaction.Create(October, Day(1), -500m, "Notebook Dell - Parcela 3/12", TransactionKind.Purchase, Now);

        transaction.MerchantName.Should().Be("Notebook Dell");
        transaction.Description.Should().Be("Notebook Dell - Parcela 3/12");
    }

    [Fact]
    public void ChangeKind_valida_o_sinal_e_atualiza_data()
    {
        var transaction = Transaction.Create(October, Day(1), 50m, "Crédito", TransactionKind.Refund, Now);
        var later = Now.AddMinutes(5);

        transaction.ChangeKind(TransactionKind.Payment, later);

        transaction.Kind.Should().Be(TransactionKind.Payment);
        transaction.UpdatedAt.Should().Be(later);
        ((Action)(() => transaction.ChangeKind(TransactionKind.Purchase, later))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Categorize_com_categoria_ativa_atribui_categoria_e_atualiza_data()
    {
        var transaction = Purchase(35.90m, "Uber");
        var category = Category.Create("Transporte");
        var later = Now.AddMinutes(5);

        transaction.Categorize(category, later);

        transaction.CategoryId.Should().Be(category.Id);
        transaction.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Categorize_com_categoria_desativada_falha()
    {
        var transaction = Purchase(35.90m, "Uber");
        var category = Category.Create("Transporte");
        category.Deactivate();

        var act = () => transaction.Categorize(category, Now);

        act.Should().Throw<DomainException>().WithMessage("*desativada*");
    }

    [Fact]
    public void RemoveCategory_limpa_categoria()
    {
        var transaction = Purchase(35.90m, "Uber");
        transaction.Categorize(Category.Create("Transporte"), Now);

        transaction.RemoveCategory(Now);

        transaction.CategoryId.Should().BeNull();
    }

    [Fact]
    public void LinkInstallment_exige_mesmo_cartao_e_parcela_valida()
    {
        var transaction = Purchase(500m, "Notebook - Parcela 3/12");
        var purchase = InstallmentPurchase.Create(Card.Id, "Notebook", 500m, 12, Day(1, 8), Now);
        var otherCard = InstallmentPurchase.Create(Guid.CreateVersion7(), "Notebook", 500m, 12, Day(1, 8), Now);

        transaction.LinkInstallment(purchase, 3);

        transaction.InstallmentPurchaseId.Should().Be(purchase.Id);
        transaction.InstallmentNumber.Should().Be(3);
        ((Action)(() => transaction.LinkInstallment(purchase, 13))).Should().Throw<DomainException>();
        ((Action)(() => transaction.LinkInstallment(otherCard, 3))).Should().Throw<DomainException>();
    }
}
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Invoices;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Application.Tests.CreditCards;

public sealed class CreditCardServiceTests : ApplicationTestBase
{
    private CreditCardService Service => Host.Get<CreditCardService>();

    [Fact]
    public async Task Lista_cartao_com_fatura_aberta_somando_compras_e_estornos_sem_pagamentos()
    {
        // Hoje: 30/09/2026; fechamento dia 3 → fatura aberta: 03/09 a 02/10, vence 10/10.
        var id = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await AddTransactionAsync(id, Day(2), -100m, "Fatura anterior");
        await AddTransactionAsync(id, Day(3), -50m, "Uber");
        await AddTransactionAsync(id, Day(25), -49.90m, "Netflix");
        await AddTransactionAsync(id, Day(26), 10m, "Estorno Uber");
        await AddTransactionAsync(id, Day(20), 800m, "Pagamento recebido");

        var card = (await Service.ListAsync(false, Ct)).Single();

        card.LastFourDigits.Should().Be("1234");
        card.Issuer.Should().Be("Nu Pagamentos");
        card.Brand.Should().Be(CardBrand.Visa);
        card.CurrentInvoice.Should().Be(new CurrentInvoiceDto(Month(10), Day(3, 10), Day(10, 10), 89.90m));
        card.LimitUsage.Should().Be(89.90m / 5000m);
    }

    [Fact]
    public async Task CreateCard_com_dia_invalido_falha()
    {
        var act = () => Service.CreateAsync(new SaveCreditCardCommand("Cartão", "Banco", CardBrand.Elo, "1234", 0m, 32, 10), Ct);

        await act.Should().ThrowAsync<DomainException>().WithMessage("O dia deve estar entre 1 e 31.");
    }

    [Fact]
    public async Task Update_e_Deactivate_alteram_o_cartao()
    {
        var id = await CreateCardAsync();

        await Service.UpdateAsync(id, new SaveCreditCardCommand("Itaú Click", "Itaú", CardBrand.Mastercard, "9876", 3000m, 5, 12), Ct);
        await Service.DeactivateAsync(id, Ct);

        (await Service.ListAsync(includeInactive: false, Ct)).Should().BeEmpty();
        var card = (await Service.ListAsync(includeInactive: true, Ct)).Single();
        card.Should().Match<CreditCardDto>(c => c.Name == "Itaú Click" && c.LastFourDigits == "9876" && c.ClosingDay == 5 && !c.IsActive);
    }

    [Fact]
    public async Task Operacao_em_cartao_inexistente_gera_erro_de_validacao()
    {
        var act = () => Service.DeactivateAsync(Guid.CreateVersion7(), Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Cartão não encontrado.");
    }
}

public sealed class InvoiceServiceTests : ApplicationTestBase
{
    private InvoiceService Service => Host.Get<InvoiceService>();

    [Fact]
    public async Task Lista_faturas_com_total_e_status()
    {
        // Hoje: 30/09/2026. Fatura de setembro (vence 10/09) já venceu; a de outubro está aberta.
        var id = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await AddTransactionAsync(id, Day(10, 8), -300m, "Mercado");
        await AddTransactionAsync(id, Day(12, 8), 20m, "Estorno");
        await AddTransactionAsync(id, Day(10), -99.90m, "Restaurante");

        var invoices = await Service.ListAsync(id, Ct);

        invoices.Select(i => (i.ReferenceMonth, i.Status, i.Total, i.TransactionCount)).Should().Equal(
            (Month(10), InvoiceStatus.Open, 99.90m, 1),
            (Month(9), InvoiceStatus.Overdue, 280m, 2));
    }

    [Fact]
    public async Task Marcar_fatura_como_paga_e_desfazer()
    {
        var id = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await AddTransactionAsync(id, Day(10, 8), -300m, "Mercado");
        var september = (await Service.ListAsync(id, Ct)).Single();

        await Service.MarkPaidAsync(september.Id, Ct);
        (await Service.ListAsync(id, Ct)).Single().Status.Should().Be(InvoiceStatus.Paid);

        await Service.MarkUnpaidAsync(september.Id, Ct);
        (await Service.ListAsync(id, Ct)).Single().Status.Should().Be(InvoiceStatus.Overdue);
    }

    [Fact]
    public async Task Fatura_inexistente_gera_erro_de_validacao()
    {
        var act = () => Service.MarkPaidAsync(Guid.CreateVersion7(), Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Fatura não encontrada.");
    }
}
using MyFinance.Application.Categories;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Application.Tests.Transactions;

public sealed class TransactionServiceTests : ApplicationTestBase
{
    private TransactionService Service => Host.Get<TransactionService>();

    private async Task<TransactionListItem> GetAsync(Guid id) =>
        (await Service.SearchAsync(new TransactionSearch(), Ct)).Items.Single(i => i.Id == id);

    [Fact]
    public async Task CategorizeTransaction_atribui_e_remove_categoria()
    {
        var card = await CreateCardAsync();
        var id = await AddTransactionAsync(card, Day(1), -89m, "IFOOD *PIZZA");
        var delivery = await CategoryIdAsync("Alimentação > Delivery");

        await Service.CategorizeAsync(id, delivery, Ct);
        var categorized = await GetAsync(id);

        categorized.CategoryId.Should().Be(delivery);
        categorized.CategoryName.Should().Be("Alimentação > Delivery");
        categorized.CategoryColor.Should().Be("#E67E22", "subcategoria herda a cor do pai");

        await Service.CategorizeAsync(id, null, Ct);
        (await GetAsync(id)).CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task CategorizeTransaction_com_categoria_desativada_falha()
    {
        var card = await CreateCardAsync();
        var id = await AddTransactionAsync(card, Day(1), -89m, "Cinema");
        var leisure = await CategoryIdAsync("Lazer");
        await Host.Get<CategoryService>().DeactivateAsync(leisure, Ct);

        var act = () => Service.CategorizeAsync(id, leisure, Ct);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*desativada*");
    }

    [Fact]
    public async Task CategorizeTransaction_com_ids_inexistentes_falha()
    {
        var card = await CreateCardAsync();
        var id = await AddTransactionAsync(card, Day(1), -1m, "x");

        var missingTransaction = () => Service.CategorizeAsync(Guid.CreateVersion7(), null, Ct);
        var missingCategory = () => Service.CategorizeAsync(id, Guid.CreateVersion7(), Ct);

        await missingTransaction.Should().ThrowAsync<ValidationException>().WithMessage("Lançamento não encontrado.");
        await missingCategory.Should().ThrowAsync<ValidationException>().WithMessage("Categoria não encontrada.");
    }

    [Fact]
    public async Task ChangeKind_altera_o_tipo_e_o_valor_de_gasto()
    {
        var card = await CreateCardAsync();
        var id = await AddTransactionAsync(card, Day(1), 50m, "Crédito");

        (await GetAsync(id)).Should().Match<TransactionListItem>(i => i.Kind == TransactionKind.Refund && i.SpendingAmount == -50m);

        await Service.ChangeKindAsync(id, TransactionKind.Payment, Ct);

        (await GetAsync(id)).Should().Match<TransactionListItem>(i => i.Kind == TransactionKind.Payment && i.SpendingAmount == 0m);

        var invalid = () => Service.ChangeKindAsync(id, TransactionKind.Purchase, Ct);
        await invalid.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Busca_filtra_por_cartao_fatura_categoria_tipo_texto_e_pagina()
    {
        // Fechamento dia 3: compras de 03/09 a 02/10 → fatura de outubro; 01/09 → setembro.
        var card = await CreateCardAsync();
        var other = await CreateCardAsync("Itaú", "9999");
        await AddTransactionAsync(card, Day(5), -300m, "Mercado Extra", "Supermercado");
        await AddTransactionAsync(card, Day(6), -40m, "Restaurante 50% off", "Alimentação > Restaurantes");
        await AddTransactionAsync(card, Day(7), -150m, "Cinema", "Lazer");
        await AddTransactionAsync(card, Day(1), -10m, "Padaria setembro");
        await AddTransactionAsync(card, Day(8), 1000m, "Pagamento recebido");
        await AddTransactionAsync(other, Day(8), -89m, "iFood");

        async Task<IReadOnlyList<string>> Search(TransactionSearch search) =>
            [.. (await Service.SearchAsync(search, Ct)).Items.Select(i => i.Description)];

        (await Search(new TransactionSearch())).Should().Equal(
            "iFood", "Pagamento recebido", "Cinema", "Restaurante 50% off", "Mercado Extra", "Padaria setembro");
        (await Search(new TransactionSearch { CreditCardId = other })).Should().Equal("iFood");
        (await Search(new TransactionSearch { FromMonth = Month(9), ToMonth = Month(9) })).Should().Equal("Padaria setembro");
        (await Search(new TransactionSearch { CategoryId = await CategoryIdAsync("Alimentação") })).Should().Equal("Restaurante 50% off");
        (await Search(new TransactionSearch { UncategorizedOnly = true })).Should().Equal("iFood", "Pagamento recebido", "Padaria setembro");
        (await Search(new TransactionSearch { Kind = TransactionKind.Payment })).Should().Equal("Pagamento recebido");
        (await Search(new TransactionSearch { Text = "mercado" })).Should().Equal("Mercado Extra");
        (await Search(new TransactionSearch { Text = "50%" })).Should().ContainSingle("o % é literal, não curinga")
            .Which.Should().Be("Restaurante 50% off");

        var page = await Service.SearchAsync(new TransactionSearch { CreditCardId = card, Page = 2, PageSize = 2 }, Ct);
        page.Items.Select(i => i.Description).Should().Equal("Restaurante 50% off", "Mercado Extra");
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);

        var item = page.Items[0];
        item.CreditCardName.Should().Be("Nubank Visa");
        item.InvoiceMonth.Should().Be(Month(10));
        item.Kind.Should().Be(TransactionKind.Purchase);
        item.SpendingAmount.Should().Be(40m);

        var byInvoice = await Service.SearchAsync(new TransactionSearch { InvoiceId = null, CreditCardId = card, FromMonth = Month(10) }, Ct);
        byInvoice.TotalCount.Should().Be(4);
    }
}
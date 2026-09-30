using MyFinance.Application.Categories;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests.Transactions;

public sealed class TransactionServiceTests : ApplicationTestBase
{
    private TransactionService Service => Host.Get<TransactionService>();

    private async Task<TransactionListItem> GetAsync(Guid id) =>
        (await Service.SearchAsync(new TransactionSearch(), Ct)).Items.Single(i => i.Id == id);

    [Fact]
    public async Task CategorizeTransaction_atribui_e_remove_categoria()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var id = await AddTransactionAsync(owner, Day(1), -89m, "IFOOD *PIZZA");
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
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var id = await AddTransactionAsync(owner, Day(1), -89m, "Cinema");
        var leisure = await CategoryIdAsync("Lazer");
        await Host.Get<CategoryService>().DeactivateAsync(leisure, Ct);

        var act = () => Service.CategorizeAsync(id, leisure, Ct);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*desativada*");
    }

    [Fact]
    public async Task CategorizeTransaction_com_ids_inexistentes_falha()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var id = await AddTransactionAsync(owner, Day(1), -1m, "x");

        var missingTransaction = () => Service.CategorizeAsync(Guid.CreateVersion7(), null, Ct);
        var missingCategory = () => Service.CategorizeAsync(id, Guid.CreateVersion7(), Ct);

        await missingTransaction.Should().ThrowAsync<ValidationException>().WithMessage("Lançamento não encontrado.");
        await missingCategory.Should().ThrowAsync<ValidationException>().WithMessage("Categoria não encontrada.");
    }

    [Fact]
    public async Task Busca_filtra_por_conta_periodo_categoria_texto_e_pagina()
    {
        var account = TransactionOwner.ForAccount(await CreateAccountAsync());
        var card = TransactionOwner.ForCreditCard(await CreateCardAsync());
        await AddTransactionAsync(account, Day(1), -300m, "Mercado Extra", "Alimentação > Mercado");
        await AddTransactionAsync(account, Day(2), -40m, "Restaurante 50% off", "Alimentação > Restaurante");
        await AddTransactionAsync(account, Day(3), -1500m, "Aluguel", "Moradia");
        await AddTransactionAsync(account, Day(1, 8), -10m, "Padaria agosto");
        await AddTransactionAsync(card, Day(4), -89m, "iFood");

        async Task<IReadOnlyList<string>> Search(TransactionSearch search) =>
            [.. (await Service.SearchAsync(search, Ct)).Items.Select(i => i.Description)];

        (await Search(new TransactionSearch())).Should().Equal("iFood", "Aluguel", "Restaurante 50% off", "Mercado Extra", "Padaria agosto");
        (await Search(new TransactionSearch { CreditCardId = card.Id })).Should().Equal("iFood");
        (await Search(new TransactionSearch { From = Day(2), To = Day(3) })).Should().Equal("Aluguel", "Restaurante 50% off");
        (await Search(new TransactionSearch { CategoryId = await CategoryIdAsync("Alimentação") }))
            .Should().Equal("Restaurante 50% off", "Mercado Extra");
        (await Search(new TransactionSearch { UncategorizedOnly = true })).Should().Equal("iFood", "Padaria agosto");
        (await Search(new TransactionSearch { Text = "mercado" })).Should().Equal("Mercado Extra");
        (await Search(new TransactionSearch { Text = "50%" })).Should().ContainSingle("o % é literal, não curinga")
            .Which.Should().Be("Restaurante 50% off");

        var page = await Service.SearchAsync(new TransactionSearch { Page = 2, PageSize = 2 }, Ct);
        page.Items.Select(i => i.Description).Should().Equal("Restaurante 50% off", "Mercado Extra");
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);

        var item = page.Items[0];
        item.OwnerType.Should().Be(TransactionOwnerType.Account);
        item.OwnerName.Should().Be("Nubank");
        item.TransactionType.Should().Be(TransactionType.Expense);
    }
}
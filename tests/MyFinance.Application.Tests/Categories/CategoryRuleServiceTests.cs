using MyFinance.Application.Categories;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;
using MyFinance.Application.Tests.Imports;
using MyFinance.Application.Transactions;

namespace MyFinance.Application.Tests.Categories;

public sealed class CategoryRuleServiceTests : ApplicationTestBase
{
    private CategoryRuleService Service => Host.Get<CategoryRuleService>();

    private async Task<TransactionListItem> FindAsync(string description) =>
        (await Host.Get<TransactionService>().SearchAsync(new TransactionSearch { Text = description }, Ct)).Items.Single();

    private async Task ImportAsync(Guid cardId, params (string Date, string Amount, string FitId, string Memo)[] rows)
    {
        var service = Host.Get<ImportService>();
        var analysis = await service.AnalyzeAsync("fatura.ofx", Text(ImportServiceTests.Ofx("1234", rows)), Ct);
        await service.ConfirmAsync(await service.PreviewAsync(analysis, cardId, null, Ct), Ct);
    }

    [Fact]
    public async Task Regras_padrao_sao_criadas_com_as_categorias_uma_unica_vez()
    {
        await Host.Get<CategoryService>().EnsureDefaultCategoriesAsync(Ct);
        await Host.Get<CategoryService>().EnsureDefaultCategoriesAsync(Ct);

        var rules = await Service.ListAsync(Ct);

        rules.Should().Contain(r => r.Pattern == "IFOOD" && r.CategoryName == "Alimentação > Delivery");
        rules.Select(r => r.Pattern).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Importacao_aplica_regras_e_deixa_sem_categoria_o_que_nao_casa()
    {
        var cardId = await CreateCardAsync();
        await Host.Get<CategoryService>().EnsureDefaultCategoriesAsync(Ct);

        await ImportAsync(cardId,
            ("20260910", "-89.00", "1", "IFOOD *PIZZARIA"),
            ("20260911", "-35.00", "2", "UBER *TRIP"),
            ("20260912", "-20.00", "3", "Padaria do Zé"),
            ("20260913", "1500.00", "4", "Pagamento recebido"));

        (await FindAsync("PIZZARIA")).CategoryName.Should().Be("Alimentação > Delivery");
        (await FindAsync("UBER")).CategoryName.Should().Be("Transporte > Aplicativos");
        (await FindAsync("Padaria")).CategoryId.Should().BeNull("nenhuma regra casa");
        (await FindAsync("Pagamento")).CategoryId.Should().BeNull("pagamento de fatura não é gasto e não é categorizado");
    }

    [Fact]
    public async Task Regra_de_maior_prioridade_vence()
    {
        var cardId = await CreateCardAsync();
        var games = await CategoryIdAsync("Entretenimento > Jogos");
        var subscriptions = await CategoryIdAsync("Assinaturas");
        await Service.CreateAsync(new SaveCategoryRuleCommand("STEAM", games, 0), Ct);
        await Service.CreateAsync(new SaveCategoryRuleCommand("STEAM ASSINATURA", subscriptions, 10), Ct);

        await ImportAsync(cardId, ("20260910", "-50.00", "1", "STEAM ASSINATURA MENSAL"), ("20260911", "-120.00", "2", "STEAM GAMES"));

        (await FindAsync("MENSAL")).CategoryName.Should().Be("Assinaturas");
        (await FindAsync("GAMES")).CategoryName.Should().Be("Entretenimento > Jogos");
    }

    [Fact]
    public async Task Regra_inativa_ou_de_categoria_desativada_e_ignorada()
    {
        var cardId = await CreateCardAsync();
        var leisure = await CategoryIdAsync("Lazer");
        var rule = await Service.CreateAsync(new SaveCategoryRuleCommand("CINEMARK", leisure, 0), Ct);
        await Service.DeactivateAsync(rule, Ct);
        await Service.CreateAsync(new SaveCategoryRuleCommand("TEATRO", await CategoryIdAsync("Viagem"), 0), Ct);
        await Host.Get<CategoryService>().DeactivateAsync(await CategoryIdAsync("Viagem"), Ct);

        await ImportAsync(cardId, ("20260910", "-50.00", "1", "CINEMARK"), ("20260911", "-80.00", "2", "TEATRO MUNICIPAL"));

        (await FindAsync("CINEMARK")).CategoryId.Should().BeNull();
        (await FindAsync("TEATRO")).CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task Aplicar_regras_categoriza_somente_lancamentos_sem_categoria()
    {
        var cardId = await CreateCardAsync();
        await AddTransactionAsync(cardId, Day(10), -60m, "Cinemark Shopping");
        await AddTransactionAsync(cardId, Day(11), -40m, "Cinemark Pipoca", "Alimentação");
        var rule = await Service.CreateAsync(new SaveCategoryRuleCommand("cinemark", await CategoryIdAsync("Lazer"), 0), Ct);

        var count = await Service.ApplyToUncategorizedAsync(Ct);

        count.Should().Be(1);
        (await FindAsync("Shopping")).CategoryName.Should().Be("Lazer");
        (await FindAsync("Pipoca")).CategoryName.Should().Be("Alimentação", "a classificação manual é preservada");
        (await Service.ListAsync(Ct)).Single(r => r.Id == rule).Pattern.Should().Be("CINEMARK");
    }

    [Fact]
    public async Task Update_e_Delete_alteram_a_regra()
    {
        var id = await Service.CreateAsync(new SaveCategoryRuleCommand("NETFLIX", await CategoryIdAsync("Lazer"), 0), Ct);

        await Service.UpdateAsync(id, new SaveCategoryRuleCommand("NETFLIX.COM", await CategoryIdAsync("Assinaturas"), 5), Ct);
        (await Service.ListAsync(Ct)).Single(r => r.Id == id)
            .Should().Match<CategoryRuleDto>(r => r.Pattern == "NETFLIX.COM" && r.CategoryName == "Assinaturas" && r.Priority == 5);

        await Service.DeleteAsync(id, Ct);
        (await Service.ListAsync(Ct)).Should().NotContain(r => r.Id == id);

        var missing = () => Service.DeleteAsync(id, Ct);
        await missing.Should().ThrowAsync<ValidationException>().WithMessage("Regra não encontrada.");
    }
}

public sealed class RuleLearningTests : ApplicationTestBase
{
    private TransactionService Service => Host.Get<TransactionService>();

    [Fact]
    public async Task Classificacao_manual_sugere_regra_para_transacoes_semelhantes()
    {
        var cardId = await CreateCardAsync();
        var id = await AddTransactionAsync(cardId, Day(10), -120m, "NUUVEM GAMES");
        await AddTransactionAsync(cardId, Day(12), -35m, "Nuuvem Games");
        await AddTransactionAsync(cardId, Day(13), -10m, "Padaria");
        var games = await CategoryIdAsync("Entretenimento > Jogos");

        var suggestion = await Service.CategorizeAsync(id, games, Ct);

        suggestion.Should().Be(new RuleSuggestion("NUUVEM GAMES", games, "Entretenimento > Jogos", MatchingUncategorized: 1));

        await Host.Get<CategoryRuleService>().CreateAsync(new SaveCategoryRuleCommand(suggestion!.Pattern, suggestion.CategoryId, 0), Ct);
        (await Host.Get<CategoryRuleService>().ApplyToUncategorizedAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Sem_sugestao_quando_uma_regra_ja_categoriza_assim_ou_ao_remover_categoria()
    {
        var cardId = await CreateCardAsync();
        var id = await AddTransactionAsync(cardId, Day(10), -89m, "IFOOD *PIZZA");
        var delivery = await CategoryIdAsync("Alimentação > Delivery");

        (await Service.CategorizeAsync(id, delivery, Ct)).Should().BeNull("a regra padrão IFOOD já leva para Delivery");
        (await Service.CategorizeAsync(id, null, Ct)).Should().BeNull();
        (await Service.CategorizeAsync(id, await CategoryIdAsync("Lazer"), Ct)).Should().NotBeNull("categoria diferente da regra existente");
    }
}
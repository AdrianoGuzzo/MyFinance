using MyFinance.Application.Categories;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Application.Tests.Categories;

public sealed class CategoryServiceTests : ApplicationTestBase
{
    private CategoryService Service => Host.Get<CategoryService>();

    [Fact]
    public async Task Categorias_padrao_sao_criadas_uma_unica_vez()
    {
        await Service.EnsureDefaultCategoriesAsync(Ct);
        await Service.EnsureDefaultCategoriesAsync(Ct);

        var categories = await Service.ListAsync(includeInactive: true, Ct);

        categories.Should().HaveCount(19);
        categories.Select(c => c.FullName).Should().ContainInOrder(
            "Alimentação", "Alimentação > Delivery", "Alimentação > Mercado", "Alimentação > Padaria", "Alimentação > Restaurante", "Assinaturas");
    }

    [Fact]
    public async Task CreateCategory_cria_categoria_e_subcategoria()
    {
        var parentId = await Service.CreateAsync(new CreateCategoryCommand("Pets", CategoryType.Expense, "#123abc"), Ct);
        var childId = await Service.CreateAsync(new CreateCategoryCommand("Veterinário", CategoryType.Income, null, parentId), Ct);

        var categories = await Service.ListAsync(false, Ct);

        categories.Should().BeEquivalentTo(
        [
            new CategoryDto(parentId, "Pets", "Pets", CategoryType.Expense, "#123ABC", null, true),
            new CategoryDto(childId, "Veterinário", "Pets > Veterinário", CategoryType.Expense, "#123ABC", parentId, true),
        ], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task CreateCategory_com_nome_repetido_no_mesmo_nivel_falha()
    {
        await Service.EnsureDefaultCategoriesAsync(Ct);
        var food = (await Service.ListAsync(false, Ct)).Single(c => c.FullName == "Alimentação").Id;

        var rootDuplicate = () => Service.CreateAsync(new CreateCategoryCommand("SAUDE", CategoryType.Expense), Ct);
        var childDuplicate = () => Service.CreateAsync(new CreateCategoryCommand("mercado", CategoryType.Expense, null, food), Ct);

        await rootDuplicate.Should().ThrowAsync<ValidationException>().WithMessage("Já existe uma categoria chamada \"SAUDE\"*");
        await childDuplicate.Should().ThrowAsync<ValidationException>();
        (await Service.CreateAsync(new CreateCategoryCommand("Mercado", CategoryType.Expense), Ct))
            .Should().NotBeEmpty("o mesmo nome em outro nível é permitido");
    }

    [Fact]
    public async Task Subcategoria_de_subcategoria_nao_e_permitida()
    {
        var parent = await Service.CreateAsync(new CreateCategoryCommand("Pets", CategoryType.Expense), Ct);
        var child = await Service.CreateAsync(new CreateCategoryCommand("Saúde pet", CategoryType.Expense, null, parent), Ct);

        var act = () => Service.CreateAsync(new CreateCategoryCommand("Vacinas", CategoryType.Expense, null, child), Ct);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Update_renomeia_e_troca_cor()
    {
        var id = await Service.CreateAsync(new CreateCategoryCommand("Pets", CategoryType.Expense), Ct);

        await Service.UpdateAsync(id, new UpdateCategoryCommand("Animais", "#00FF00"), Ct);

        (await Service.ListAsync(false, Ct)).Single().Should().Match<CategoryDto>(c => c.Name == "Animais" && c.Color == "#00FF00");
    }

    [Fact]
    public async Task Desativar_categoria_desativa_subcategorias_e_reativar_exige_pai_ativo()
    {
        var parent = await Service.CreateAsync(new CreateCategoryCommand("Pets", CategoryType.Expense), Ct);
        var child = await Service.CreateAsync(new CreateCategoryCommand("Ração", CategoryType.Expense, null, parent), Ct);

        await Service.DeactivateAsync(parent, Ct);

        (await Service.ListAsync(includeInactive: false, Ct)).Should().BeEmpty();
        var activateChild = () => Service.ActivateAsync(child, Ct);
        await activateChild.Should().ThrowAsync<ValidationException>().WithMessage("Ative primeiro a categoria principal.");

        await Service.ActivateAsync(parent, Ct);
        await Service.ActivateAsync(child, Ct);
        (await Service.ListAsync(includeInactive: false, Ct)).Should().HaveCount(2);
    }
}
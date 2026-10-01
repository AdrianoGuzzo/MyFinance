using MyFinance.Application.Categories;
using MyFinance.Application.Common.Exceptions;
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

        categories.Should().HaveCount(20);
        categories.Select(c => c.FullName).Should().ContainInOrder(
            "Alimentação", "Alimentação > Delivery", "Alimentação > Padaria", "Alimentação > Restaurantes", "Assinaturas");
    }

    [Fact]
    public async Task CreateCategory_cria_categoria_e_subcategoria()
    {
        var parentId = await Service.CreateAsync(new CreateCategoryCommand("Pets", "#123abc"), Ct);
        var childId = await Service.CreateAsync(new CreateCategoryCommand("Veterinário", null, parentId), Ct);

        var categories = await Service.ListAsync(false, Ct);

        categories.Should().BeEquivalentTo(
        [
            new CategoryDto(parentId, "Pets", "Pets", "#123ABC", null, true),
            new CategoryDto(childId, "Veterinário", "Pets > Veterinário", "#123ABC", parentId, true),
        ], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task CreateCategory_com_nome_repetido_no_mesmo_nivel_falha()
    {
        await Service.EnsureDefaultCategoriesAsync(Ct);
        var food = (await Service.ListAsync(false, Ct)).Single(c => c.FullName == "Alimentação").Id;

        var rootDuplicate = () => Service.CreateAsync(new CreateCategoryCommand("SAUDE"), Ct);
        var childDuplicate = () => Service.CreateAsync(new CreateCategoryCommand("delivery", null, food), Ct);

        await rootDuplicate.Should().ThrowAsync<ValidationException>().WithMessage("Já existe uma categoria chamada \"SAUDE\"*");
        await childDuplicate.Should().ThrowAsync<ValidationException>();
        (await Service.CreateAsync(new CreateCategoryCommand("Delivery"), Ct))
            .Should().NotBeEmpty("o mesmo nome em outro nível é permitido");
    }

    [Fact]
    public async Task Subcategoria_de_subcategoria_nao_e_permitida()
    {
        var parent = await Service.CreateAsync(new CreateCategoryCommand("Pets"), Ct);
        var child = await Service.CreateAsync(new CreateCategoryCommand("Saúde pet", null, parent), Ct);

        var act = () => Service.CreateAsync(new CreateCategoryCommand("Vacinas", null, child), Ct);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Update_renomeia_e_troca_cor()
    {
        var id = await Service.CreateAsync(new CreateCategoryCommand("Pets"), Ct);

        await Service.UpdateAsync(id, new UpdateCategoryCommand("Animais", "#00FF00"), Ct);

        (await Service.ListAsync(false, Ct)).Single().Should().Match<CategoryDto>(c => c.Name == "Animais" && c.Color == "#00FF00");
    }

    [Fact]
    public async Task Desativar_categoria_desativa_subcategorias_e_reativar_exige_pai_ativo()
    {
        var parent = await Service.CreateAsync(new CreateCategoryCommand("Pets"), Ct);
        var child = await Service.CreateAsync(new CreateCategoryCommand("Ração", null, parent), Ct);

        await Service.DeactivateAsync(parent, Ct);

        (await Service.ListAsync(includeInactive: false, Ct)).Should().BeEmpty();
        var activateChild = () => Service.ActivateAsync(child, Ct);
        await activateChild.Should().ThrowAsync<ValidationException>().WithMessage("Ative primeiro a categoria principal.");

        await Service.ActivateAsync(parent, Ct);
        await Service.ActivateAsync(child, Ct);
        (await Service.ListAsync(includeInactive: false, Ct)).Should().HaveCount(2);
    }
}
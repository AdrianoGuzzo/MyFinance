using MyFinance.Domain.Entities;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Tests.Entities;

public sealed class CategoryTests
{
    [Fact]
    public void Create_categoria_raiz_ativa()
    {
        var category = Category.Create(" Alimentação ", HexColor.Create("#e67e22"));

        category.Name.Should().Be("Alimentação");
        category.Color!.Value.Should().Be("#E67E22");
        category.IsActive.Should().BeTrue();
        category.IsSubcategory.Should().BeFalse();
    }

    [Fact]
    public void Create_sem_nome_falha()
    {
        var act = () => Category.Create(" ");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateSubcategory_herda_cor_do_pai()
    {
        var parent = Category.Create("Alimentação", HexColor.Create("#E67E22"));

        var child = parent.CreateSubcategory("Delivery");

        child.ParentCategoryId.Should().Be(parent.Id);
        child.Color.Should().Be(parent.Color);
        child.IsSubcategory.Should().BeTrue();
    }

    [Fact]
    public void CreateSubcategory_de_subcategoria_falha()
    {
        var child = Category.Create("Alimentação").CreateSubcategory("Delivery");

        var act = () => child.CreateSubcategory("iFood");

        act.Should().Throw<DomainException>().WithMessage("*subcategoria de uma subcategoria*");
    }

    [Fact]
    public void CreateSubcategory_em_categoria_desativada_falha()
    {
        var parent = Category.Create("Alimentação");
        parent.Deactivate();

        var act = () => parent.CreateSubcategory("Delivery");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Rename_e_Deactivate_alteram_estado()
    {
        var category = Category.Create("Lazer");

        category.Rename("Diversão");
        category.Deactivate();

        category.Name.Should().Be("Diversão");
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public void DefaultCategories_contem_somente_categorias_de_gastos()
    {
        var categories = DefaultCategories.Create();

        categories.Where(c => !c.IsSubcategory).Select(c => c.Name).Should().Equal(
            "Alimentação", "Supermercado", "Transporte", "Assinaturas", "Lazer", "Entretenimento", "Compras",
            "Saúde", "Educação", "Moradia", "Viagem", "Serviços", "Tarifas e juros", "Outros");

        var food = categories.Single(c => c.Name == "Alimentação");
        categories.Where(c => c.ParentCategoryId == food.Id).Select(c => c.Name)
            .Should().Equal("Restaurantes", "Delivery", "Padaria");

        categories.Select(c => c.Name).Should().NotContain(["Salário", "Investimentos", "Transferências"]);
    }

    [Fact]
    public void DefaultCategories_regras_padrao_apontam_para_categorias_existentes()
    {
        var categories = DefaultCategories.Create();

        var rules = DefaultCategories.CreateRules(categories, TestData.Now);

        rules.Should().NotBeEmpty();
        rules.Select(r => r.CategoryId).Should().OnlyContain(id => categories.Any(c => c.Id == id));
        var delivery = categories.Single(c => c.Name == "Delivery");
        rules.Single(r => r.Pattern == "IFOOD").CategoryId.Should().Be(delivery.Id);
    }
}
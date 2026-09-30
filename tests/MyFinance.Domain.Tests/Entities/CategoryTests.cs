using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Tests.Entities;

public sealed class CategoryTests
{
    [Fact]
    public void Create_categoria_raiz_ativa()
    {
        var category = Category.Create(" Alimentação ", CategoryType.Expense, HexColor.Create("#e67e22"));

        category.Name.Should().Be("Alimentação");
        category.Type.Should().Be(CategoryType.Expense);
        category.Color!.Value.Should().Be("#E67E22");
        category.IsActive.Should().BeTrue();
        category.IsSubcategory.Should().BeFalse();
    }

    [Fact]
    public void Create_sem_nome_falha()
    {
        var act = () => Category.Create(" ", CategoryType.Expense);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateSubcategory_herda_tipo_e_cor_do_pai()
    {
        var parent = Category.Create("Alimentação", CategoryType.Expense, HexColor.Create("#E67E22"));

        var child = parent.CreateSubcategory("Delivery");

        child.ParentCategoryId.Should().Be(parent.Id);
        child.Type.Should().Be(CategoryType.Expense);
        child.Color.Should().Be(parent.Color);
        child.IsSubcategory.Should().BeTrue();
    }

    [Fact]
    public void CreateSubcategory_de_subcategoria_falha()
    {
        var child = Category.Create("Alimentação", CategoryType.Expense).CreateSubcategory("Delivery");

        var act = () => child.CreateSubcategory("iFood");

        act.Should().Throw<DomainException>().WithMessage("*subcategoria de uma subcategoria*");
    }

    [Fact]
    public void CreateSubcategory_em_categoria_desativada_falha()
    {
        var parent = Category.Create("Alimentação", CategoryType.Expense);
        parent.Deactivate();

        var act = () => parent.CreateSubcategory("Delivery");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Rename_e_Deactivate_alteram_estado()
    {
        var category = Category.Create("Lazer", CategoryType.Expense);

        category.Rename("Diversão");
        category.Deactivate();

        category.Name.Should().Be("Diversão");
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public void DefaultCategories_contem_categorias_padrao_e_subcategorias_de_alimentacao()
    {
        var categories = DefaultCategories.Create();

        categories.Where(c => !c.IsSubcategory).Select(c => c.Name).Should().Equal(
            "Alimentação", "Transporte", "Moradia", "Saúde", "Educação", "Lazer",
            "Assinaturas", "Compras", "Impostos", "Salário", "Investimentos", "Outros", "Transferências");

        var food = categories.Single(c => c.Name == "Alimentação");
        categories.Where(c => c.ParentCategoryId == food.Id).Select(c => c.Name)
            .Should().Equal("Mercado", "Restaurante", "Delivery", "Padaria");

        categories.Single(c => c.Name == "Salário").Type.Should().Be(CategoryType.Income);

        var transfers = categories.Single(c => c.Name == "Transferências");
        transfers.Type.Should().Be(CategoryType.Transfer);
        categories.Where(c => c.ParentCategoryId == transfers.Id).Select(c => c.Name)
            .Should().Equal("Pagamento de fatura", "Entre contas");
    }
}
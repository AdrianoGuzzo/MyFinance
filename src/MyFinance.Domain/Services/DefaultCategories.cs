using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Services;

/// <summary>Categorias criadas no primeiro uso da aplicação.</summary>
public static class DefaultCategories
{
    private static readonly (string Name, CategoryType Type, string Color, string[] Children)[] Definitions =
    [
        ("Alimentação", CategoryType.Expense, "#E67E22", ["Mercado", "Restaurante", "Delivery", "Padaria"]),
        ("Transporte", CategoryType.Expense, "#3498DB", []),
        ("Moradia", CategoryType.Expense, "#8E44AD", []),
        ("Saúde", CategoryType.Expense, "#E74C3C", []),
        ("Educação", CategoryType.Expense, "#16A085", []),
        ("Lazer", CategoryType.Expense, "#F1C40F", []),
        ("Assinaturas", CategoryType.Expense, "#9B59B6", []),
        ("Compras", CategoryType.Expense, "#D35400", []),
        ("Impostos", CategoryType.Expense, "#7F8C8D", []),
        ("Salário", CategoryType.Income, "#27AE60", []),
        ("Investimentos", CategoryType.Income, "#2ECC71", []),
        ("Outros", CategoryType.Expense, "#95A5A6", []),
        ("Transferências", CategoryType.Transfer, "#5D6D7E", ["Pagamento de fatura", "Entre contas"]),
    ];

    /// <summary>Categorias raiz seguidas de suas subcategorias.</summary>
    public static IReadOnlyList<Category> Create()
    {
        var categories = new List<Category>();
        foreach (var (name, type, color, children) in Definitions)
        {
            var root = Category.Create(name, type, HexColor.Create(color));
            categories.Add(root);
            categories.AddRange(children.Select(child => root.CreateSubcategory(child)));
        }

        return categories;
    }
}
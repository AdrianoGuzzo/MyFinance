using MyFinance.Domain.Entities;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Services;

/// <summary>Categorias de gastos e regras de categorização criadas no primeiro uso da aplicação.</summary>
public static class DefaultCategories
{
    private static readonly (string Name, string Color, string[] Children)[] Definitions =
    [
        ("Alimentação", "#E67E22", ["Restaurantes", "Delivery", "Padaria"]),
        ("Supermercado", "#27AE60", []),
        ("Transporte", "#3498DB", ["Aplicativos", "Combustível"]),
        ("Assinaturas", "#9B59B6", []),
        ("Lazer", "#F1C40F", []),
        ("Entretenimento", "#E84393", ["Jogos"]),
        ("Compras", "#D35400", []),
        ("Saúde", "#E74C3C", []),
        ("Educação", "#16A085", []),
        ("Moradia", "#8E44AD", []),
        ("Viagem", "#00A8A8", []),
        ("Serviços", "#5D6D7E", []),
        ("Tarifas e juros", "#7F8C8D", []),
        ("Outros", "#95A5A6", []),
    ];

    /// <summary>Padrão → "Categoria" ou "Categoria &gt; Subcategoria".</summary>
    private static readonly (string Pattern, string Category)[] Rules =
    [
        ("IFOOD", "Alimentação > Delivery"),
        ("IFD", "Alimentação > Delivery"),
        ("RAPPI", "Alimentação > Delivery"),
        ("CARREFOUR", "Supermercado"),
        ("ASSAI", "Supermercado"),
        ("PAO DE ACUCAR", "Supermercado"),
        ("UBER", "Transporte > Aplicativos"),
        ("99APP", "Transporte > Aplicativos"),
        ("STEAM", "Entretenimento > Jogos"),
        ("PLAYSTATION", "Entretenimento > Jogos"),
        ("XBOX", "Entretenimento > Jogos"),
        ("NETFLIX", "Assinaturas"),
        ("SPOTIFY", "Assinaturas"),
        ("DISNEY", "Assinaturas"),
        ("APPLE.COM/BILL", "Assinaturas"),
        ("AMAZON PRIME", "Assinaturas"),
        ("IOF", "Tarifas e juros"),
        ("ANUIDADE", "Tarifas e juros"),
    ];

    /// <summary>Categorias principais seguidas de suas subcategorias.</summary>
    public static IReadOnlyList<Category> Create()
    {
        var categories = new List<Category>();
        foreach (var (name, color, children) in Definitions)
        {
            var root = Category.Create(name, HexColor.Create(color));
            categories.Add(root);
            categories.AddRange(children.Select(child => root.CreateSubcategory(child)));
        }

        return categories;
    }

    /// <summary>
    /// Regras padrão para as categorias informadas (normalmente as criadas por <see cref="Create"/>).
    /// Regras cuja categoria não existe são ignoradas.
    /// </summary>
    public static IReadOnlyList<CategoryRule> CreateRules(IReadOnlyList<Category> categories, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var byId = categories.ToDictionary(c => c.Id);
        var byFullName = categories
            .Where(c => c.IsActive)
            .ToDictionary(
                c => c.ParentCategoryId is { } p && byId.TryGetValue(p, out var parent) ? $"{parent.Name} > {c.Name}" : c.Name,
                StringComparer.OrdinalIgnoreCase);

        return [.. Rules
            .Where(r => byFullName.ContainsKey(r.Category))
            .Select(r => CategoryRule.Create(r.Pattern, byFullName[r.Category], priority: 0, nowUtc))];
    }
}
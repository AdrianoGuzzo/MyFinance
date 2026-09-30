using MyFinance.Domain.Entities;

namespace MyFinance.Domain.Analysis;

/// <summary>Categoria usada para agrupar gastos; <see cref="Id"/> nulo = "Sem categoria".</summary>
public sealed record CategoryKey(Guid? Id, string Name, string Color)
{
    public const string UncategorizedName = "Sem categoria";
    public const string UncategorizedColor = "#95A5A6";

    public static CategoryKey Uncategorized { get; } = new(null, UncategorizedName, UncategorizedColor);
}

/// <summary>Nível de agrupamento das análises por categoria.</summary>
public enum CategoryLevel
{
    /// <summary>Categoria principal (subcategorias somam no pai).</summary>
    Root = 1,

    /// <summary>Categoria atribuída ao lançamento (subcategoria, quando houver).</summary>
    Leaf = 2,
}

/// <summary>Resolve a categoria principal ou a própria categoria de um lançamento, com nome e cor para exibição.</summary>
public sealed class CategoryLookup
{
    private const string DefaultColor = CategoryKey.UncategorizedColor;
    private readonly Dictionary<Guid, Category> _byId;

    public CategoryLookup(IEnumerable<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        _byId = categories.ToDictionary(c => c.Id);
    }

    public CategoryKey Resolve(Guid? categoryId, CategoryLevel level)
    {
        if (categoryId is not { } id || !_byId.TryGetValue(id, out var category))
        {
            return CategoryKey.Uncategorized;
        }

        var parent = category.ParentCategoryId is { } parentId ? _byId.GetValueOrDefault(parentId) : null;
        if (level == CategoryLevel.Root && parent is not null)
        {
            return Key(parent, parent.Name);
        }

        return Key(category, level == CategoryLevel.Leaf && parent is not null ? $"{parent.Name} > {category.Name}" : category.Name);
    }

    /// <summary>A categoria e, se for principal, suas subcategorias.</summary>
    public IReadOnlySet<Guid> WithChildren(Guid categoryId) =>
        _byId.Values.Where(c => c.Id == categoryId || c.ParentCategoryId == categoryId).Select(c => c.Id).ToHashSet();

    private static CategoryKey Key(Category category, string name) => new(category.Id, name, category.Color?.Value ?? DefaultColor);
}
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Categories;

public sealed record CreateCategoryCommand(string Name, string? Color = null, Guid? ParentCategoryId = null);

public sealed record UpdateCategoryCommand(string Name, string? Color);

/// <param name="FullName">Ex.: "Alimentação &gt; Delivery".</param>
public sealed record CategoryDto(
    Guid Id,
    string Name,
    string FullName,
    string? Color,
    Guid? ParentCategoryId,
    bool IsActive);

public sealed class CategoryService(
    ICategoryRepository categories,
    ICategoryRuleRepository rules,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var color = ParseColor(command.Color);
        Category category;

        if (command.ParentCategoryId is { } parentId)
        {
            var parent = await GetAsync(parentId, cancellationToken);
            category = parent.CreateSubcategory(command.Name, color);
        }
        else
        {
            category = Category.Create(command.Name, color);
        }

        await EnsureUniqueNameAsync(category.Name, category.ParentCategoryId, null, cancellationToken);

        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await GetAsync(id, cancellationToken);
        category.Rename(command.Name);
        category.ChangeColor(ParseColor(command.Color));
        await EnsureUniqueNameAsync(category.Name, category.ParentCategoryId, id, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Desativa a categoria e suas subcategorias. Lançamentos já categorizados são mantidos.</summary>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await GetAsync(id, cancellationToken);
        category.Deactivate();

        foreach (var child in (await categories.ListAsync(includeInactive: false, cancellationToken)).Where(c => c.ParentCategoryId == id))
        {
            child.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await GetAsync(id, cancellationToken);

        if (category.ParentCategoryId is { } parentId && !(await GetAsync(parentId, cancellationToken)).IsActive)
        {
            throw new ValidationException("Ative primeiro a categoria principal.");
        }

        category.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Categorias ordenadas como árvore: cada principal seguida de suas subcategorias.</summary>
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var all = await categories.ListAsync(includeInactive, cancellationToken);
        return ToTree(all);
    }

    /// <summary>Cria as categorias e as regras de categorização padrão no primeiro uso. Idempotente.</summary>
    public async Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var defaults = DefaultCategories.Create();
        categories.AddRange(defaults);
        rules.AddRange(DefaultCategories.CreateRules(defaults, timeProvider.GetUtcNow().UtcDateTime));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static IReadOnlyList<CategoryDto> ToTree(IReadOnlyList<Category> all)
    {
        var comparer = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);
        var result = new List<CategoryDto>();

        foreach (var root in all.Where(c => !c.IsSubcategory).OrderBy(c => c.Name, comparer))
        {
            result.Add(ToDto(root, null));
            result.AddRange(all
                .Where(c => c.ParentCategoryId == root.Id)
                .OrderBy(c => c.Name, comparer)
                .Select(c => ToDto(c, root)));
        }

        return result;
    }

    internal static CategoryDto ToDto(Category category, Category? parent) => new(
        category.Id,
        category.Name,
        parent is null ? category.Name : $"{parent.Name} > {category.Name}",
        category.Color?.Value,
        category.ParentCategoryId,
        category.IsActive);

    private async Task EnsureUniqueNameAsync(string name, Guid? parentId, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await categories.NameExistsAsync(name, parentId, exceptId, cancellationToken))
        {
            throw new ValidationException($"Já existe uma categoria chamada \"{name}\" neste nível.");
        }
    }

    private async Task<Category> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await categories.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Categoria não encontrada.");

    private static HexColor? ParseColor(string? color) => string.IsNullOrWhiteSpace(color) ? null : HexColor.Create(color);
}
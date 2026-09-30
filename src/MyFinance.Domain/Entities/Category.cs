using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Categoria de lançamentos. Suporta um nível de subcategorias (ex.: Alimentação &gt; Mercado).
/// </summary>
public sealed class Category
{
    public const int NameMaxLength = 60;

    private Category() { } // EF Core

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CategoryType Type { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    public HexColor? Color { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsSubcategory => ParentCategoryId.HasValue;

    public static Category Create(string name, CategoryType type, HexColor? color = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = Guard.Required(name, NameMaxLength, "O nome da categoria"),
        Type = Guard.Defined(type, "Tipo de categoria"),
        Color = color,
        IsActive = true,
    };

    /// <summary>Cria uma subcategoria. Herda o tipo e, se não informada, a cor da categoria pai.</summary>
    public Category CreateSubcategory(string name, HexColor? color = null)
    {
        if (IsSubcategory)
        {
            throw new DomainException("Não é possível criar subcategoria de uma subcategoria.");
        }

        if (!IsActive)
        {
            throw new DomainException("Não é possível criar subcategoria em uma categoria desativada.");
        }

        var child = Create(name, Type, color ?? Color);
        child.ParentCategoryId = Id;
        return child;
    }

    public void Rename(string name) => Name = Guard.Required(name, NameMaxLength, "O nome da categoria");

    public void ChangeColor(HexColor? color) => Color = color;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public override string ToString() => Name;
}
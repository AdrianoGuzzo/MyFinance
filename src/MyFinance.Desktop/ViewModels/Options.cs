using MyFinance.Desktop.Services;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Cartão selecionável (importação, filtros); <see cref="Id"/> nulo = "todos".</summary>
public sealed record CardOption(Guid? Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Categoria selecionável; <see cref="Id"/> nulo = "sem categoria".</summary>
public sealed record CategoryOption(Guid? Id, string Label)
{
    public override string ToString() => Label;
}

public static class Options
{
    public static IReadOnlyList<Option<CardBrand>> CardBrands { get; } = All<CardBrand>();

    public static IReadOnlyList<Option<TransactionKind>> TransactionKinds { get; } = All<TransactionKind>();

    public static IReadOnlyList<Option<RecurringClassification>> RecurringClassifications { get; } = All<RecurringClassification>();

    public static IReadOnlyList<Option<AppTheme>> Themes { get; } =
    [
        new(AppTheme.System, "Seguir o sistema"),
        new(AppTheme.Light, "Claro"),
        new(AppTheme.Dark, "Escuro"),
    ];

    private static Option<T>[] All<T>()
        where T : struct, Enum =>
        [.. Enum.GetValues<T>().Select(v => new Option<T>(v, Labels.Describe(v)))];
}
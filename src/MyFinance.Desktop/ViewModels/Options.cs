using MyFinance.Desktop.Services;
using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Desktop.ViewModels;

public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Conta ou cartão selecionável (importação, filtros).</summary>
public sealed record OwnerOption(TransactionOwner Owner, string Label)
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
    public static IReadOnlyList<Option<AccountType>> AccountTypes { get; } =
    [
        new(AccountType.Checking, Labels.For(AccountType.Checking)),
        new(AccountType.Savings, Labels.For(AccountType.Savings)),
        new(AccountType.Payment, Labels.For(AccountType.Payment)),
        new(AccountType.Investment, Labels.For(AccountType.Investment)),
    ];

    public static IReadOnlyList<Option<CategoryType>> CategoryTypes { get; } =
    [
        new(CategoryType.Expense, Labels.For(CategoryType.Expense)),
        new(CategoryType.Income, Labels.For(CategoryType.Income)),
        new(CategoryType.Transfer, Labels.For(CategoryType.Transfer)),
    ];

    public static IReadOnlyList<Option<AppTheme>> Themes { get; } =
    [
        new(AppTheme.System, "Seguir o sistema"),
        new(AppTheme.Light, "Claro"),
        new(AppTheme.Dark, "Escuro"),
    ];
}
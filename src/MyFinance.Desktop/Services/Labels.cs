using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.Services;

/// <summary>Textos exibidos para valores de enums.</summary>
public static class Labels
{
    public static string For(AccountType value) => value switch
    {
        AccountType.Checking => "Conta corrente",
        AccountType.Savings => "Poupança",
        AccountType.Payment => "Conta de pagamento",
        AccountType.Investment => "Investimentos",
        _ => value.ToString(),
    };

    public static string For(CategoryType value) => value switch
    {
        CategoryType.Expense => "Despesa",
        CategoryType.Income => "Receita",
        CategoryType.Transfer => "Transferência",
        _ => value.ToString(),
    };

    public static string For(ImportTransactionStatus value) => value switch
    {
        ImportTransactionStatus.New => "Nova",
        ImportTransactionStatus.Duplicate => "Duplicada",
        ImportTransactionStatus.Invalid => "Erro",
        ImportTransactionStatus.Imported => "Importada",
        _ => value.ToString(),
    };

    public static string For(DuplicateReason value) => value switch
    {
        DuplicateReason.ExternalId => "mesmo identificador do banco",
        DuplicateReason.ImportHash => "mesmos dados já importados",
        DuplicateReason.SameData => "mesma data, valor e descrição",
        DuplicateReason.RepeatedInFile => "repetida no próprio arquivo",
        _ => string.Empty,
    };

    public static string Describe(object? value) => value switch
    {
        AccountType v => For(v),
        CategoryType v => For(v),
        ImportTransactionStatus v => For(v),
        DuplicateReason v => For(v),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };
}
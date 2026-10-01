using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.Services;

/// <summary>Textos exibidos para valores de enums.</summary>
public static class Labels
{
    public static string For(CardBrand value) => value switch
    {
        CardBrand.Visa => "Visa",
        CardBrand.Mastercard => "Mastercard",
        CardBrand.Elo => "Elo",
        CardBrand.Amex => "American Express",
        CardBrand.Hipercard => "Hipercard",
        CardBrand.Other => "Outra",
        _ => value.ToString(),
    };

    public static string For(TransactionKind value) => value switch
    {
        TransactionKind.Purchase => "Compra",
        TransactionKind.Refund => "Estorno",
        TransactionKind.Payment => "Pagamento",
        TransactionKind.Fee => "Tarifa",
        TransactionKind.Interest => "Juros",
        TransactionKind.Adjustment => "Ajuste",
        _ => value.ToString(),
    };

    public static string For(InvoiceStatus value) => value switch
    {
        InvoiceStatus.Open => "Aberta",
        InvoiceStatus.Closed => "Fechada",
        InvoiceStatus.Paid => "Paga",
        InvoiceStatus.Overdue => "Vencida",
        _ => value.ToString(),
    };

    public static string For(LimitStatus value) => value switch
    {
        LimitStatus.Within => "Dentro do limite",
        LimitStatus.Near => "Próximo do limite",
        LimitStatus.Exceeded => "Limite excedido",
        _ => value.ToString(),
    };

    public static string For(RecurringClassification value) => value switch
    {
        RecurringClassification.Unclassified => "Não classificado",
        RecurringClassification.Essential => "Essencial",
        RecurringClassification.Optional => "Opcional",
        RecurringClassification.Evaluate => "Avaliar",
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
        CardBrand v => For(v),
        TransactionKind v => For(v),
        InvoiceStatus v => For(v),
        LimitStatus v => For(v),
        RecurringClassification v => For(v),
        ImportTransactionStatus v => For(v),
        DuplicateReason v => For(v),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };
}
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;

namespace MyFinance.Domain.Tests.Analysis;

/// <summary>Categorias e lançamentos fictícios para os testes de análise.</summary>
internal static class AnalysisTestData
{
    public static readonly Category Food = Category.Create("Alimentação");
    public static readonly Category Delivery = Food.CreateSubcategory("Delivery");
    public static readonly Category Restaurants = Food.CreateSubcategory("Restaurantes");
    public static readonly Category Leisure = Category.Create("Lazer");
    public static readonly Category Market = Category.Create("Supermercado");

    public static readonly CategoryLookup Lookup = new([Food, Delivery, Restaurants, Leisure, Market]);

    public static DateOnly M(int month, int year = 2026) => new(year, month, 1);

    /// <summary>Gasto (positivo) na fatura do mês.</summary>
    public static SpendingEntry Spend(int month, decimal spending, Category? category = null, string merchant = "LOJA",
        Guid? installmentPurchaseId = null, int? installmentNumber = null) =>
        new(Guid.CreateVersion7(), Guid.Empty, Guid.Empty, M(month), M(month), -spending,
            spending >= 0 ? TransactionKind.Purchase : TransactionKind.Refund,
            category?.Id, merchant, merchant, installmentPurchaseId, installmentNumber);

    public static SpendingEntry Payment(int month, decimal amount) =>
        new(Guid.CreateVersion7(), Guid.Empty, Guid.Empty, M(month), M(month), amount, TransactionKind.Payment, null, "PAGAMENTO", "Pagamento", null, null);
}
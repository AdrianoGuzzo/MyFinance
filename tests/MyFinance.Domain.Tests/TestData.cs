using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Cartão que fecha dia 3 e vence dia 10.</summary>
    public static readonly CreditCard Card = NewCard(closingDay: 3, dueDay: 10);

    /// <summary>Fatura de outubro/2026 de <see cref="Card"/> (compras de 03/09 a 02/10).</summary>
    public static readonly Invoice October = Invoice.For(Card, Card.GetInvoicePeriodForMonth(Day(1, 10)));

    public static DateOnly Day(int day, int month = 9, int year = 2026) => new(year, month, day);

    public static CreditCard NewCard(int closingDay, int dueDay, string lastFour = "1234") => CreditCard.Create(
        "Nubank", "Nu Pagamentos", CardBrand.Mastercard, LastFourDigits.Create(lastFour), 5000m,
        DayOfMonth.Create(closingDay), DayOfMonth.Create(dueDay), Now);

    public static Transaction Purchase(decimal amount, string description = "Compra", Invoice? invoice = null) =>
        Transaction.Create(invoice ?? October, Day(10), -amount, description, TransactionKind.Purchase, Now);
}
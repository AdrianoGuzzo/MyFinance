using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    public static readonly TransactionOwner AccountOwner = TransactionOwner.ForAccount(Guid.CreateVersion7());

    public static DateOnly Day(int day, int month = 9, int year = 2026) => new(year, month, day);
}
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

public sealed class Account
{
    public const int NameMaxLength = 100;
    public const int BankNameMaxLength = 100;
    public const int AgencyMaxLength = 10;

    private Account() { } // EF Core

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string BankName { get; private set; } = string.Empty;

    public AccountNumber? AccountNumber { get; private set; }

    public string? Agency { get; private set; }

    public AccountType AccountType { get; private set; }

    public decimal InitialBalance { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsActive { get; private set; }

    public static Account Create(
        string name,
        string bankName,
        AccountType accountType,
        decimal initialBalance,
        DateTime createdAtUtc,
        AccountNumber? accountNumber = null,
        string? agency = null)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = Guard.Utc(createdAtUtc),
            IsActive = true,
        };
        account.Update(name, bankName, accountType, initialBalance, accountNumber, agency);
        return account;
    }

    public void Update(
        string name,
        string bankName,
        AccountType accountType,
        decimal initialBalance,
        AccountNumber? accountNumber,
        string? agency)
    {
        Name = Guard.Required(name, NameMaxLength, "O nome da conta");
        BankName = Guard.Required(bankName, BankNameMaxLength, "O nome do banco");
        AccountType = Guard.Defined(accountType, "Tipo de conta");
        InitialBalance = Guard.Money(initialBalance, "O saldo inicial");
        AccountNumber = accountNumber;
        Agency = Guard.Optional(agency, AgencyMaxLength, "A agência");
    }

    /// <summary>Saldo = saldo inicial + soma dos lançamentos da conta.</summary>
    public decimal BalanceWith(decimal transactionsTotal) => InitialBalance + transactionsTotal;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public override string ToString() => $"{Name} ({BankName})";
}
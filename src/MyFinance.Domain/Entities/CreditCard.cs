using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Cartão de crédito. Compras no cartão são lançamentos do cartão (não da conta bancária)
/// e se agrupam em faturas calculadas a partir do dia de fechamento e de vencimento.
/// </summary>
public sealed class CreditCard
{
    public const int NameMaxLength = 100;
    public const int BankNameMaxLength = 100;

    private CreditCard() { } // EF Core

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string BankName { get; private set; } = string.Empty;

    public LastFourDigits LastFourDigits { get; private set; } = null!;

    public decimal CreditLimit { get; private set; }

    public DayOfMonth ClosingDay { get; private set; }

    public DayOfMonth DueDay { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsActive { get; private set; }

    public static CreditCard Create(
        string name,
        string bankName,
        LastFourDigits lastFourDigits,
        decimal creditLimit,
        DayOfMonth closingDay,
        DayOfMonth dueDay,
        DateTime createdAtUtc)
    {
        var card = new CreditCard
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = Guard.Utc(createdAtUtc),
            IsActive = true,
        };
        card.Update(name, bankName, lastFourDigits, creditLimit, closingDay, dueDay);
        return card;
    }

    public void Update(
        string name,
        string bankName,
        LastFourDigits lastFourDigits,
        decimal creditLimit,
        DayOfMonth closingDay,
        DayOfMonth dueDay)
    {
        ArgumentNullException.ThrowIfNull(lastFourDigits);

        if (creditLimit < 0)
        {
            throw new DomainException("O limite do cartão não pode ser negativo.");
        }

        Name = Guard.Required(name, NameMaxLength, "O nome do cartão");
        BankName = Guard.Required(bankName, BankNameMaxLength, "O nome do banco");
        LastFourDigits = lastFourDigits;
        CreditLimit = Guard.Money(creditLimit, "O limite");
        ClosingDay = closingDay;
        DueDay = dueDay;
    }

    /// <summary>
    /// Fatura à qual pertence uma compra feita em <paramref name="purchaseDate"/>.
    /// Compras a partir do dia de fechamento entram na fatura seguinte.
    /// O vencimento cai no mesmo mês do fechamento quando o dia de vencimento é posterior
    /// ao de fechamento; caso contrário, no mês seguinte.
    /// </summary>
    public InvoicePeriod GetInvoicePeriod(DateOnly purchaseDate)
    {
        var closingThisMonth = ClosingDay.In(purchaseDate);
        var closing = purchaseDate < closingThisMonth ? closingThisMonth : ClosingDay.In(purchaseDate.AddMonths(1));
        var start = ClosingDay.In(closing.AddMonths(-1));
        var due = DueDay.Value > ClosingDay.Value ? DueDay.In(closing) : DueDay.In(closing.AddMonths(1));
        return new InvoicePeriod(start, closing, due);
    }

    /// <summary>Fatura aberta (ainda não fechada) na data informada.</summary>
    public InvoicePeriod GetCurrentInvoicePeriod(DateOnly today) => GetInvoicePeriod(today);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public override string ToString() => $"{Name} {LastFourDigits}";
}
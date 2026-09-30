using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Cartão de crédito. Os lançamentos pertencem a faturas calculadas a partir do dia de fechamento e de vencimento.
/// </summary>
public sealed class CreditCard
{
    public const int NameMaxLength = 100;
    public const int IssuerMaxLength = 100;

    private CreditCard() { } // EF Core

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Instituição emissora (ex.: Nubank, Itaú).</summary>
    public string Issuer { get; private set; } = string.Empty;

    public CardBrand Brand { get; private set; }

    public LastFourDigits LastFourDigits { get; private set; } = null!;

    public decimal CreditLimit { get; private set; }

    public DayOfMonth ClosingDay { get; private set; }

    public DayOfMonth DueDay { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsActive { get; private set; }

    public static CreditCard Create(
        string name,
        string issuer,
        CardBrand brand,
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
        card.Update(name, issuer, brand, lastFourDigits, creditLimit, closingDay, dueDay);
        return card;
    }

    public void Update(
        string name,
        string issuer,
        CardBrand brand,
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
        Issuer = Guard.Required(issuer, IssuerMaxLength, "A instituição");
        Brand = Guard.Defined(brand, "Bandeira");
        LastFourDigits = lastFourDigits;
        CreditLimit = Guard.Money(creditLimit, "O limite");
        ClosingDay = closingDay;
        DueDay = dueDay;
    }

    /// <summary>
    /// Fatura à qual pertence um lançamento feito em <paramref name="date"/>.
    /// Lançamentos a partir do dia de fechamento entram na fatura seguinte.
    /// O vencimento cai no mesmo mês do fechamento quando o dia de vencimento é posterior
    /// ao de fechamento; caso contrário, no mês seguinte.
    /// </summary>
    public InvoicePeriod GetInvoicePeriod(DateOnly date)
    {
        var closingThisMonth = ClosingDay.In(date);
        var closing = date < closingThisMonth ? closingThisMonth : ClosingDay.In(date.AddMonths(1));
        return PeriodClosingIn(closing);
    }

    /// <summary>Fatura aberta (ainda não fechada) na data informada.</summary>
    public InvoicePeriod GetCurrentInvoicePeriod(DateOnly today) => GetInvoicePeriod(today);

    /// <summary>Fatura cujo mês de referência (mês de vencimento) é <paramref name="referenceMonth"/>.</summary>
    public InvoicePeriod GetInvoicePeriodForMonth(DateOnly referenceMonth)
    {
        var dueMonth = Months.Of(referenceMonth);
        var closingMonth = DueDay.Value > ClosingDay.Value ? dueMonth : dueMonth.AddMonths(-1);
        return PeriodClosingIn(ClosingDay.In(closingMonth));
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public override string ToString() => $"{Name} {LastFourDigits}";

    private InvoicePeriod PeriodClosingIn(DateOnly closing)
    {
        var start = ClosingDay.In(closing.AddMonths(-1));
        var due = DueDay.Value > ClosingDay.Value ? DueDay.In(closing) : DueDay.In(closing.AddMonths(1));
        return new InvoicePeriod(start, closing, due);
    }
}
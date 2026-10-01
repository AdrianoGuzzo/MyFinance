using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;

namespace MyFinance.Domain.Entities;

/// <summary>Parcela prevista de uma compra parcelada: número, mês da fatura e valor.</summary>
public sealed record InstallmentSlot(int Number, DateOnly InvoiceMonth, decimal Amount);

/// <summary>
/// Compra parcelada. Cada parcela importada é um <see cref="Transaction"/> vinculado a ela;
/// a compra conhece o cronograma completo e permite projetar o comprometimento das faturas futuras.
/// Diferencia o valor total (ex.: R$ 6.000 em 12x) do impacto mensal (R$ 500 por fatura).
/// </summary>
public sealed class InstallmentPurchase
{
    public const int DescriptionMaxLength = MerchantNormalizer.MaxLength;

    private InstallmentPurchase() { } // EF Core

    public Guid Id { get; private set; }

    public Guid CreditCardId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Estabelecimento normalizado, usado para vincular as parcelas seguintes.</summary>
    public string MerchantKey { get; private set; } = string.Empty;

    /// <summary>Valor de cada parcela (positivo).</summary>
    public decimal InstallmentAmount { get; private set; }

    public int InstallmentCount { get; private set; }

    public decimal TotalAmount { get; private set; }

    /// <summary>Mês de referência da fatura da 1ª parcela.</summary>
    public DateOnly FirstInvoiceMonth { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateOnly LastInvoiceMonth => FirstInvoiceMonth.AddMonths(InstallmentCount - 1);

    /// <param name="installmentAmount">Valor de uma parcela, positivo.</param>
    /// <param name="firstInvoiceMonth">Mês da fatura da 1ª parcela.</param>
    public static InstallmentPurchase Create(
        Guid creditCardId,
        string description,
        decimal installmentAmount,
        int installmentCount,
        DateOnly firstInvoiceMonth,
        DateTime createdAtUtc)
    {
        if (creditCardId == Guid.Empty)
        {
            throw new DomainException("O cartão da compra parcelada é obrigatório.");
        }

        if (installmentAmount <= 0)
        {
            throw new DomainException("O valor da parcela deve ser positivo.");
        }

        if (installmentCount is < 2 or > InstallmentParser.MaxInstallments)
        {
            throw new DomainException($"A quantidade de parcelas deve estar entre 2 e {InstallmentParser.MaxInstallments}.");
        }

        var name = Guard.Required(description, int.MaxValue, "A descrição da compra");
        var merchant = MerchantNormalizer.Normalize(name);

        return new InstallmentPurchase
        {
            Id = Guid.CreateVersion7(),
            CreditCardId = creditCardId,
            Description = merchant.Name,
            MerchantKey = merchant.Key,
            InstallmentAmount = Guard.Money(installmentAmount, "O valor da parcela"),
            InstallmentCount = installmentCount,
            TotalAmount = installmentAmount * installmentCount,
            FirstInvoiceMonth = Months.Of(firstInvoiceMonth),
            CreatedAt = Guard.Utc(createdAtUtc),
        };
    }

    /// <summary>Mês da fatura da parcela <paramref name="number"/>.</summary>
    public DateOnly InvoiceMonthOf(int number) => FirstInvoiceMonth.AddMonths(number - 1);

    /// <summary>Número da parcela que cai na fatura de <paramref name="month"/>, ou <c>null</c> se nenhuma.</summary>
    public int? NumberIn(DateOnly month)
    {
        var number = Months.Between(FirstInvoiceMonth, Months.Of(month)) + 1;
        return number >= 1 && number <= InstallmentCount ? number : null;
    }

    public IReadOnlyList<InstallmentSlot> Schedule() =>
        [.. Enumerable.Range(1, InstallmentCount).Select(n => new InstallmentSlot(n, InvoiceMonthOf(n), InstallmentAmount))];

    /// <summary>Parcelas em faturas posteriores a <paramref name="month"/>.</summary>
    public int RemainingCount(DateOnly month) =>
        Math.Clamp(Months.Between(Months.Of(month), LastInvoiceMonth), 0, InstallmentCount);

    /// <summary>Valor das parcelas em faturas posteriores a <paramref name="month"/>.</summary>
    public decimal RemainingAmount(DateOnly month) => RemainingCount(month) * InstallmentAmount;

}
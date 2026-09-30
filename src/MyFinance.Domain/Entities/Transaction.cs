using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Lançamento de uma fatura de cartão de crédito.
/// Convenção de sinal: negativo = débito no cartão (compra, tarifa, juros); positivo = crédito (estorno, pagamento).
/// O valor que conta como gasto é <see cref="SpendingAmount"/>.
/// </summary>
public sealed class Transaction
{
    public const int DescriptionMaxLength = 500;
    public const int ExternalIdMaxLength = 255;
    public const int MerchantMaxLength = MerchantNormalizer.MaxLength;

    private Transaction() { } // EF Core

    public Guid Id { get; private set; }

    public Guid CreditCardId { get; private set; }

    public Guid InvoiceId { get; private set; }

    /// <summary>Data informada pelo arquivo (compra ou lançamento).</summary>
    public DateOnly Date { get; private set; }

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Estabelecimento para exibição (sem sufixo de parcela, prefixos de intermediadores...).</summary>
    public string MerchantName { get; private set; } = string.Empty;

    /// <summary>Estabelecimento normalizado, usado para agrupar.</summary>
    public string MerchantKey { get; private set; } = string.Empty;

    public TransactionKind Kind { get; private set; }

    /// <summary>Identificador fornecido pelo banco (ex.: FITID do OFX), quando existir.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Hash dos dados relevantes na importação; usado na detecção de duplicidades.</summary>
    public Sha256Hash? ImportHash { get; private set; }

    /// <summary>Categoria ou subcategoria. A categoria principal é derivada da subcategoria.</summary>
    public Guid? CategoryId { get; private set; }

    public Guid? InstallmentPurchaseId { get; private set; }

    public int? InstallmentNumber { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Quanto o lançamento representa em gastos (ver <see cref="Spending.AmountOf"/>).</summary>
    public decimal SpendingAmount => Spending.AmountOf(Kind, Amount);

    public static Transaction Create(
        Invoice invoice,
        DateOnly date,
        decimal amount,
        string description,
        TransactionKind kind,
        DateTime createdAtUtc,
        string? externalId = null,
        Sha256Hash? importHash = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (date == default)
        {
            throw new DomainException("A data do lançamento é obrigatória.");
        }

        if (amount == 0)
        {
            throw new DomainException("O valor do lançamento não pode ser zero.");
        }

        var transaction = new Transaction
        {
            Id = Guid.CreateVersion7(),
            CreditCardId = invoice.CreditCardId,
            InvoiceId = invoice.Id,
            Date = date,
            Amount = Guard.Money(amount, "O valor do lançamento"),
            ExternalId = Guard.Optional(externalId, ExternalIdMaxLength, "O identificador externo"),
            ImportHash = importHash,
            CreatedAt = Guard.Utc(createdAtUtc),
        };
        transaction.SetDescription(description);
        transaction.Kind = EnsureKindMatchesSign(kind, transaction.Amount);
        return transaction;
    }

    /// <summary>Valida se o tipo é compatível com o sinal do valor, sem criar o lançamento.</summary>
    public static TransactionKind EnsureKindMatchesSign(TransactionKind kind, decimal amount)
    {
        Guard.Defined(kind, "Tipo de lançamento");

        var ok = kind switch
        {
            TransactionKind.Purchase or TransactionKind.Fee or TransactionKind.Interest => amount < 0,
            TransactionKind.Refund or TransactionKind.Payment => amount > 0,
            _ => true,
        };

        return ok
            ? kind
            : throw new DomainException(amount < 0
                ? "Estornos e pagamentos devem ter valor positivo (crédito no cartão)."
                : "Compras, tarifas e juros devem ter valor negativo (débito no cartão).");
    }

    public void Categorize(Category category, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (!category.IsActive)
        {
            throw new DomainException($"A categoria \"{category.Name}\" está desativada.");
        }

        CategoryId = category.Id;
        Touch(nowUtc);
    }

    public void RemoveCategory(DateTime nowUtc)
    {
        CategoryId = null;
        Touch(nowUtc);
    }

    public void ChangeKind(TransactionKind kind, DateTime nowUtc)
    {
        Kind = EnsureKindMatchesSign(kind, Amount);
        Touch(nowUtc);
    }

    public void ChangeDescription(string description, DateTime nowUtc)
    {
        SetDescription(description);
        Touch(nowUtc);
    }

    /// <summary>Vincula este lançamento como a parcela <paramref name="number"/> de uma compra parcelada.</summary>
    public void LinkInstallment(InstallmentPurchase purchase, int number)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        if (purchase.CreditCardId != CreditCardId)
        {
            throw new DomainException("A parcela deve pertencer ao mesmo cartão da compra parcelada.");
        }

        if (number < 1 || number > purchase.InstallmentCount)
        {
            throw new DomainException($"Parcela {number} fora do intervalo 1–{purchase.InstallmentCount}.");
        }

        InstallmentPurchaseId = purchase.Id;
        InstallmentNumber = number;
    }

    private void SetDescription(string description)
    {
        Description = Guard.Required(description, DescriptionMaxLength, "A descrição do lançamento");
        var merchant = MerchantNormalizer.Normalize(Description);
        MerchantName = merchant.Name;
        MerchantKey = merchant.Key;
    }

    private void Touch(DateTime nowUtc) => UpdatedAt = Guard.Utc(nowUtc);
}
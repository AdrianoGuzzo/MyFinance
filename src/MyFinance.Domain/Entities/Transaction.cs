using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Entities;

/// <summary>
/// Lançamento financeiro de uma conta bancária ou de um cartão de crédito.
/// Convenção de sinal: valor positivo = entrada; negativo = saída (compra, débito, pagamento).
/// </summary>
public sealed class Transaction
{
    public const int DescriptionMaxLength = 500;
    public const int ExternalIdMaxLength = 255;

    private Transaction() { } // EF Core

    public Guid Id { get; private set; }

    public Guid? AccountId { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Identificador fornecido pelo banco (ex.: FITID do OFX), quando existir.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Hash dos dados relevantes na importação; usado na detecção de duplicidades.</summary>
    public Sha256Hash? ImportHash { get; private set; }

    public Guid? CategoryId { get; private set; }

    public TransactionType TransactionType { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public TransactionOwner Owner => TransactionOwner.From(AccountId, CreditCardId);

    public static Transaction Create(
        TransactionOwner owner,
        DateOnly date,
        decimal amount,
        string description,
        DateTime createdAtUtc,
        string? externalId = null,
        Sha256Hash? importHash = null)
    {
        if (owner.Id == Guid.Empty)
        {
            throw new DomainException("A conta ou cartão do lançamento é obrigatório.");
        }

        if (date == default)
        {
            throw new DomainException("A data do lançamento é obrigatória.");
        }

        if (amount == 0)
        {
            throw new DomainException("O valor do lançamento não pode ser zero.");
        }

        return new Transaction
        {
            Id = Guid.CreateVersion7(),
            AccountId = owner.AccountId,
            CreditCardId = owner.CreditCardId,
            Date = date,
            Amount = Guard.Money(amount, "O valor do lançamento"),
            TransactionType = amount > 0 ? TransactionType.Income : TransactionType.Expense,
            Description = Guard.Required(description, DescriptionMaxLength, "A descrição do lançamento"),
            ExternalId = Guard.Optional(externalId, ExternalIdMaxLength, "O identificador externo"),
            ImportHash = importHash,
            CreatedAt = Guard.Utc(createdAtUtc),
        };
    }

    public bool IsIncome => TransactionType == TransactionType.Income;

    public bool IsExpense => TransactionType == TransactionType.Expense;

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

    public void ChangeDescription(string description, DateTime nowUtc)
    {
        Description = Guard.Required(description, DescriptionMaxLength, "A descrição do lançamento");
        Touch(nowUtc);
    }

    private void Touch(DateTime nowUtc) => UpdatedAt = Guard.Utc(nowUtc);
}
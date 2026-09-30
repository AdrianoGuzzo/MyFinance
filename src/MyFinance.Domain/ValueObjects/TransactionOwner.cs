using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.ValueObjects;

/// <summary>
/// Onde o lançamento acontece: uma conta bancária ou um cartão de crédito (nunca ambos).
/// </summary>
public readonly record struct TransactionOwner
{
    private TransactionOwner(TransactionOwnerType type, Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A conta ou cartão do lançamento é obrigatório.");
        }

        Type = type;
        Id = id;
    }

    public TransactionOwnerType Type { get; }

    public Guid Id { get; }

    public Guid? AccountId => Type == TransactionOwnerType.Account ? Id : null;

    public Guid? CreditCardId => Type == TransactionOwnerType.CreditCard ? Id : null;

    public static TransactionOwner ForAccount(Guid accountId) => new(TransactionOwnerType.Account, accountId);

    public static TransactionOwner ForCreditCard(Guid creditCardId) => new(TransactionOwnerType.CreditCard, creditCardId);

    public static TransactionOwner From(Guid? accountId, Guid? creditCardId) => (accountId, creditCardId) switch
    {
        ({ } a, null) => ForAccount(a),
        (null, { } c) => ForCreditCard(c),
        _ => throw new DomainException("O lançamento deve pertencer a uma conta ou a um cartão, nunca a ambos."),
    };
}
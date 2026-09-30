using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.CreditCards;

public sealed record SaveCreditCardCommand(
    string Name,
    string BankName,
    string LastFourDigits,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay);

/// <param name="Amount">
/// Total de compras do período (positivo). No MVP, pagamentos e estornos não são abatidos:
/// a fatura como entidade (saldo anterior, pagamentos, ajustes) fica para a fase 2 (ADR 0005).
/// </param>
public sealed record InvoiceDto(DateOnly StartDate, DateOnly ClosingDate, DateOnly DueDate, DateOnly ReferenceMonth, decimal Amount);

public sealed record CreditCardDto(
    Guid Id,
    string Name,
    string BankName,
    string LastFourDigits,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay,
    bool IsActive,
    InvoiceDto CurrentInvoice);

public sealed class CreditCardService(
    ICreditCardRepository creditCards,
    ITransactionQueries queries,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(SaveCreditCardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var card = CreditCard.Create(
            command.Name,
            command.BankName,
            LastFourDigits.Create(command.LastFourDigits),
            command.CreditLimit,
            DayOfMonth.Create(command.ClosingDay),
            DayOfMonth.Create(command.DueDay),
            timeProvider.GetUtcNow().UtcDateTime);

        creditCards.Add(card);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return card.Id;
    }

    public async Task UpdateAsync(Guid id, SaveCreditCardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var card = await GetAsync(id, cancellationToken);
        card.Update(
            command.Name,
            command.BankName,
            LastFourDigits.Create(command.LastFourDigits),
            command.CreditLimit,
            DayOfMonth.Create(command.ClosingDay),
            DayOfMonth.Create(command.DueDay));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, c => c.Deactivate(), cancellationToken);

    public Task ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, c => c.Activate(), cancellationToken);

    public async Task<IReadOnlyList<CreditCardDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var result = new List<CreditCardDto>();

        foreach (var card in await creditCards.ListAsync(includeInactive, cancellationToken))
        {
            var invoice = await GetInvoiceAsync(card, card.GetCurrentInvoicePeriod(today), queries, cancellationToken);
            result.Add(new CreditCardDto(
                card.Id,
                card.Name,
                card.BankName,
                card.LastFourDigits.Value,
                card.CreditLimit,
                card.ClosingDay.Value,
                card.DueDay.Value,
                card.IsActive,
                invoice));
        }

        return result;
    }

    internal static async Task<InvoiceDto> GetInvoiceAsync(
        CreditCard card, InvoicePeriod period, ITransactionQueries queries, CancellationToken cancellationToken)
    {
        var purchases = await queries.SumOutflowsAsync(
            TransactionOwner.ForCreditCard(card.Id), period.StartDate, period.ClosingDate.AddDays(-1), cancellationToken);

        // Compras são negativas; a fatura é exibida como valor a pagar (positivo).
        return new InvoiceDto(period.StartDate, period.ClosingDate, period.DueDate, period.ReferenceMonth, -purchases);
    }

    private async Task ChangeAsync(Guid id, Action<CreditCard> change, CancellationToken cancellationToken)
    {
        change(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<CreditCard> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await creditCards.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Cartão não encontrado.");
}
using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.CreditCards;

public sealed record SaveCreditCardCommand(
    string Name,
    string Issuer,
    CardBrand Brand,
    string LastFourDigits,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay);

/// <param name="Amount">Total da fatura aberta até agora (compras, tarifas e juros menos estornos).</param>
public sealed record CurrentInvoiceDto(DateOnly ReferenceMonth, DateOnly ClosingDate, DateOnly DueDate, decimal Amount);

/// <param name="LimitUsage">Fatura aberta / limite (1 = 100%); <c>null</c> sem limite informado.</param>
public sealed record CreditCardDto(
    Guid Id,
    string Name,
    string Issuer,
    CardBrand Brand,
    string LastFourDigits,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay,
    bool IsActive,
    CurrentInvoiceDto CurrentInvoice,
    decimal? LimitUsage);

public sealed class CreditCardService(
    ICreditCardRepository creditCards,
    ISpendingQueries spending,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(SaveCreditCardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var card = CreditCard.Create(
            command.Name,
            command.Issuer,
            command.Brand,
            LastFourDigits.Create(command.LastFourDigits),
            command.CreditLimit,
            DayOfMonth.Create(command.ClosingDay),
            DayOfMonth.Create(command.DueDay),
            timeProvider.UtcNow());

        creditCards.Add(card);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return card.Id;
    }

    /// <summary>
    /// Alterar fechamento/vencimento vale para as próximas faturas; faturas já criadas mantêm suas datas.
    /// </summary>
    public async Task UpdateAsync(Guid id, SaveCreditCardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var card = await GetAsync(id, cancellationToken);
        card.Update(
            command.Name,
            command.Issuer,
            command.Brand,
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
        var today = timeProvider.Today();
        var cards = await creditCards.ListAsync(includeInactive, cancellationToken);
        if (cards.Count == 0)
        {
            return [];
        }

        var periods = cards.ToDictionary(c => c.Id, c => c.GetCurrentInvoicePeriod(today));
        var entries = await spending.GetEntriesAsync(
            periods.Values.Min(p => p.ReferenceMonth), periods.Values.Max(p => p.ReferenceMonth), null, cancellationToken);

        return [.. cards.Select(card =>
        {
            var period = periods[card.Id];
            var amount = entries
                .Where(e => e.CreditCardId == card.Id && e.InvoiceMonth == period.ReferenceMonth)
                .Sum(e => e.Spending);

            return new CreditCardDto(
                card.Id,
                card.Name,
                card.Issuer,
                card.Brand,
                card.LastFourDigits.Value,
                card.CreditLimit,
                card.ClosingDay.Value,
                card.DueDay.Value,
                card.IsActive,
                new CurrentInvoiceDto(period.ReferenceMonth, period.ClosingDate, period.DueDate, amount),
                card.CreditLimit > 0 ? amount / card.CreditLimit : null);
        })];
    }

    private async Task ChangeAsync(Guid id, Action<CreditCard> change, CancellationToken cancellationToken)
    {
        change(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<CreditCard> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await creditCards.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Cartão não encontrado.");
}
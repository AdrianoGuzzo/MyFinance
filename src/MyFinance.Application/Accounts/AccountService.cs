using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Accounts;

public sealed record SaveAccountCommand(
    string Name,
    string BankName,
    AccountType AccountType,
    decimal InitialBalance,
    string? AccountNumber = null,
    string? Agency = null);

/// <param name="AccountNumber">Value object; <c>ToString()</c> é mascarado. Use <c>.Value</c> apenas em formulários de edição.</param>
public sealed record AccountDto(
    Guid Id,
    string Name,
    string BankName,
    AccountType AccountType,
    AccountNumber? AccountNumber,
    string? Agency,
    decimal InitialBalance,
    decimal Balance,
    bool IsActive);

public sealed class AccountService(
    IAccountRepository accounts,
    ITransactionQueries queries,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(SaveAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = Account.Create(
            command.Name,
            command.BankName,
            command.AccountType,
            command.InitialBalance,
            timeProvider.GetUtcNow().UtcDateTime,
            ParseAccountNumber(command.AccountNumber),
            command.Agency);

        accounts.Add(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task UpdateAsync(Guid id, SaveAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await GetAsync(id, cancellationToken);
        account.Update(
            command.Name,
            command.BankName,
            command.AccountType,
            command.InitialBalance,
            ParseAccountNumber(command.AccountNumber),
            command.Agency);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, a => a.Deactivate(), cancellationToken);

    public Task ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, a => a.Activate(), cancellationToken);

    public async Task<IReadOnlyList<AccountDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var list = await accounts.ListAsync(includeInactive, cancellationToken);
        var totals = await queries.GetAccountTotalsAsync(null, cancellationToken);

        return [.. list.Select(a => new AccountDto(
            a.Id,
            a.Name,
            a.BankName,
            a.AccountType,
            a.AccountNumber,
            a.Agency,
            a.InitialBalance,
            a.BalanceWith(totals.GetValueOrDefault(a.Id)),
            a.IsActive))];
    }

    private async Task ChangeAsync(Guid id, Action<Account> change, CancellationToken cancellationToken)
    {
        change(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Account> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await accounts.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Conta não encontrada.");

    private static AccountNumber? ParseAccountNumber(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : AccountNumber.Create(value);
}
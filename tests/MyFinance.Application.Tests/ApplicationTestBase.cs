using System.Text;

using Microsoft.Extensions.DependencyInjection;

using MyFinance.Application.Accounts;
using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests;

public abstract class ApplicationTestBase : IAsyncLifetime
{
    private TestHost? _host;

    protected CancellationToken Ct { get; } = TestContext.Current.CancellationToken;

    internal TestHost Host => _host ?? throw new InvalidOperationException("Host não inicializado.");

    public async ValueTask InitializeAsync() => _host = await TestHost.CreateAsync(Ct, ConfigureServices);

    private protected virtual void ConfigureServices(IServiceCollection services) { }

    /// <summary>Grava um lançamento diretamente (sem importação) e, se informado, o categoriza pelo nome completo.</summary>
    protected async Task<Guid> AddTransactionAsync(
        TransactionOwner owner, DateOnly date, decimal amount, string description, string? categoryFullName = null)
    {
        using (var scope = Host.CreateScope())
        {
            var transaction = Transaction.Create(owner, date, amount, description, Host.Clock.GetUtcNow().UtcDateTime);
            scope.ServiceProvider.GetRequiredService<ITransactionRepository>().AddRange([transaction]);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

            if (categoryFullName is not null)
            {
                await Host.Get<TransactionService>().CategorizeAsync(transaction.Id, await CategoryIdAsync(categoryFullName), Ct);
            }

            return transaction.Id;
        }
    }

    protected async Task<Guid> CategoryIdAsync(string fullName)
    {
        await Host.Get<CategoryService>().EnsureDefaultCategoriesAsync(Ct);
        var categories = await Host.Get<CategoryService>().ListAsync(includeInactive: true, Ct);
        return categories.Single(c => c.FullName == fullName).Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    protected static DateOnly Day(int day, int month = 9, int year = 2026) => new(year, month, day);

    protected static Stream Text(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    protected Task<Guid> CreateAccountAsync(string name = "Nubank", decimal initialBalance = 0m, string? number = "99999999-9") =>
        Host.Get<AccountService>().CreateAsync(
            new SaveAccountCommand(name, "Nu Pagamentos", AccountType.Payment, initialBalance, number, "0001"), Ct);

    protected Task<Guid> CreateCardAsync(string name = "Nubank Visa", string lastFour = "1234", int closingDay = 3, int dueDay = 10) =>
        Host.Get<CreditCardService>().CreateAsync(
            new SaveCreditCardCommand(name, "Nu Pagamentos", lastFour, 5000m, closingDay, dueDay), Ct);
}
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Invoices;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;

namespace MyFinance.Application.Tests;

public abstract class ApplicationTestBase : IAsyncLifetime
{
    private TestHost? _host;

    protected CancellationToken Ct { get; } = TestContext.Current.CancellationToken;

    internal TestHost Host => _host ?? throw new InvalidOperationException("Host não inicializado.");

    public async ValueTask InitializeAsync() => _host = await TestHost.CreateAsync(Ct, ConfigureServices);

    private protected virtual void ConfigureServices(IServiceCollection services) { }

    /// <summary>
    /// Grava um lançamento diretamente (sem importação), na fatura correspondente à data, e, se informado,
    /// o categoriza pelo nome completo. O tipo padrão é sugerido pelo sinal e pela descrição.
    /// </summary>
    protected async Task<Guid> AddTransactionAsync(
        Guid creditCardId, DateOnly date, decimal amount, string description, string? categoryFullName = null, TransactionKind? kind = null)
    {
        using (var scope = Host.CreateScope())
        {
            var services = scope.ServiceProvider;
            var card = await services.GetRequiredService<ICreditCardRepository>().GetByIdAsync(creditCardId, Ct)
                ?? throw new InvalidOperationException("Cartão não encontrado.");
            var month = card.GetInvoicePeriod(date).ReferenceMonth;
            var invoices = await InvoiceBook.EnsureAsync(card, [month], services.GetRequiredService<IInvoiceRepository>(), Ct);

            var transaction = Transaction.Create(
                invoices[month], date, amount, description, kind ?? TransactionKindClassifier.Classify(amount, description),
                Host.Clock.GetUtcNow().UtcDateTime);
            services.GetRequiredService<ITransactionRepository>().AddRange([transaction]);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

            if (categoryFullName is not null)
            {
                await Host.Get<TransactionService>().CategorizeAsync(transaction.Id, await CategoryIdAsync(categoryFullName), Ct);
            }

            return transaction.Id;
        }
    }

    /// <summary>Compra (valor positivo = gasto) na fatura do mês de referência informado.</summary>
    protected async Task<Guid> SpendAsync(Guid creditCardId, DateOnly invoiceMonth, decimal spending, string description, string? categoryFullName = null)
    {
        var card = await CardAsync(creditCardId);
        var date = card.GetInvoicePeriodForMonth(invoiceMonth).StartDate;
        return await AddTransactionAsync(creditCardId, date, -spending, description, categoryFullName, TransactionKind.Purchase);
    }

    protected async Task<CreditCard> CardAsync(Guid creditCardId)
    {
        using var scope = Host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICreditCardRepository>().GetByIdAsync(creditCardId, Ct)
            ?? throw new InvalidOperationException("Cartão não encontrado.");
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

    /// <summary>Mês (primeiro dia).</summary>
    protected static DateOnly Month(int month, int year = 2026) => new(year, month, 1);

    protected static Stream Text(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    protected Task<Guid> CreateCardAsync(string name = "Nubank Visa", string lastFour = "1234", int closingDay = 3, int dueDay = 10) =>
        Host.Get<CreditCardService>().CreateAsync(
            new SaveCreditCardCommand(name, "Nu Pagamentos", CardBrand.Visa, lastFour, 5000m, closingDay, dueDay), Ct);
}
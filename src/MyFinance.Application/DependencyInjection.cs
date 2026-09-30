using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Dashboard;
using MyFinance.Application.Imports;
using MyFinance.Application.Invoices;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICategorizationService, NoCategorizationService>();

        services.AddScoped<CreditCardService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<ImportService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
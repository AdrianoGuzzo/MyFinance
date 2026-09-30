using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MyFinance.Application.Accounts;
using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Dashboard;
using MyFinance.Application.Imports;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICategorizationService, NoCategorizationService>();

        services.AddScoped<AccountService>();
        services.AddScoped<CreditCardService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<ImportService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
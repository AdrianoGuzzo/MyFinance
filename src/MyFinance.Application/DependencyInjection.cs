using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MyFinance.Application.Analysis;
using MyFinance.Application.Categories;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Dashboard;
using MyFinance.Application.Imports;
using MyFinance.Application.Installments;
using MyFinance.Application.Invoices;
using MyFinance.Application.Reports;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICategorizationService, RuleBasedCategorizationService>();

        services.AddScoped<CreditCardService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CategoryRuleService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<ImportService>();
        services.AddScoped<AnalysisLoader>();
        services.AddScoped<InstallmentService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
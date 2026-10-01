using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MyFinance.Application.Analysis;
using MyFinance.Application.Backup;
using MyFinance.Application.Imports;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Interfaces;
using MyFinance.Infrastructure.Imports.CSV;
using MyFinance.Infrastructure.Imports.OFX;
using MyFinance.Infrastructure.Persistence;
using MyFinance.Infrastructure.Persistence.Queries;
using MyFinance.Infrastructure.Persistence.Repositories;

namespace MyFinance.Infrastructure;

public static class DependencyInjection
{
    /// <param name="databasePath">Caminho do arquivo SQLite. Vazio = <see cref="SqliteConnectionFactory.DefaultDatabasePath"/>.</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? databasePath)
    {
        var path = string.IsNullOrWhiteSpace(databasePath) ? SqliteConnectionFactory.DefaultDatabasePath() : databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(SqliteConnectionFactory.BuildConnectionString(path)));
        return services.AddPersistenceServices().AddImporters();
    }

    /// <summary>Importadores de arquivo (sem estado) e o seletor de formato.</summary>
    public static IServiceCollection AddImporters(this IServiceCollection services)
    {
        services.AddSingleton<ITransactionImporter, OfxTransactionImporter>();
        services.AddSingleton<ITransactionImporter, CsvTransactionImporter>();
        services.AddSingleton<TransactionImporterResolver>();
        return services;
    }

    /// <summary>Repositórios e unidade de trabalho (sem registrar o DbContext; útil em testes).</summary>
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryRuleRepository, CategoryRuleRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IInstallmentPurchaseRepository, InstallmentPurchaseRepository>();
        services.AddScoped<ISpendingLimitRepository, SpendingLimitRepository>();
        services.AddScoped<IFinancialGoalRepository, FinancialGoalRepository>();
        services.AddScoped<IRecurringExpenseRepository, RecurringExpenseRepository>();
        services.AddScoped<IImportRepository, ImportRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransactionQueries, TransactionQueries>();
        services.AddScoped<ISpendingQueries, SpendingQueries>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IDatabaseBackup, SqliteDatabaseBackup>();
        return services;
    }
}
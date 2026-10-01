using Microsoft.EntityFrameworkCore;

using MyFinance.Domain.Entities;
using MyFinance.Domain.ValueObjects;
using MyFinance.Infrastructure.Persistence.Converters;

namespace MyFinance.Infrastructure.Persistence;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InstallmentPurchase> InstallmentPurchases => Set<InstallmentPurchase>();

    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();

    public DbSet<SpendingLimit> SpendingLimits => Set<SpendingLimit>();

    public DbSet<FinancialGoal> FinancialGoals => Set<FinancialGoal>();

    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Import> Imports => Set<Import>();

    public DbSet<ImportTransaction> ImportTransactions => Set<ImportTransaction>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<Sha256Hash>().HaveConversion<Sha256HashConverter>().HaveMaxLength(64);
        configurationBuilder.Properties<HexColor>().HaveConversion<HexColorConverter>().HaveMaxLength(7);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
}
using Microsoft.EntityFrameworkCore;

using MyFinance.Domain.Entities;
using MyFinance.Domain.ValueObjects;
using MyFinance.Infrastructure.Persistence.Converters;

namespace MyFinance.Infrastructure.Persistence;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<CreditCard> CreditCards => Set<CreditCard>();

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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;
using MyFinance.Infrastructure.Persistence.Converters;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).HasMaxLength(Account.NameMaxLength).IsRequired();
        builder.Property(a => a.BankName).HasMaxLength(Account.BankNameMaxLength).IsRequired();
        builder.Property(a => a.AccountNumber).HasConversion<AccountNumberConverter>().HasMaxLength(22);
        builder.Property(a => a.Agency).HasMaxLength(Account.AgencyMaxLength);
        builder.Property(a => a.AccountType).IsRequired();
        builder.Property(a => a.InitialBalance).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
    }
}
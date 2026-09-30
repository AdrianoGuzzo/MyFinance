using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;
using MyFinance.Infrastructure.Persistence.Converters;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("CreditCards", t =>
        {
            t.HasCheckConstraint("CK_CreditCards_ClosingDay", "\"ClosingDay\" BETWEEN 1 AND 31");
            t.HasCheckConstraint("CK_CreditCards_DueDay", "\"DueDay\" BETWEEN 1 AND 31");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(CreditCard.NameMaxLength).IsRequired();
        builder.Property(c => c.BankName).HasMaxLength(CreditCard.BankNameMaxLength).IsRequired();
        builder.Property(c => c.LastFourDigits).HasConversion<LastFourDigitsConverter>().HasMaxLength(4).IsRequired();
        builder.Property(c => c.CreditLimit).IsRequired();
        builder.Property(c => c.ClosingDay).HasConversion<DayOfMonthConverter>().IsRequired();
        builder.Property(c => c.DueDay).HasConversion<DayOfMonthConverter>().IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
    }
}
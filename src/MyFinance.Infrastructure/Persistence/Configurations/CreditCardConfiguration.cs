using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
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
            t.HasCheckConstraint("CK_CreditCards_Brand", CheckConstraints.EnumIn<CardBrand>("Brand"));
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(CreditCard.NameMaxLength).IsRequired();
        builder.Property(c => c.Issuer).HasMaxLength(CreditCard.IssuerMaxLength).IsRequired();
        builder.Property(c => c.Brand).IsRequired();
        builder.Property(c => c.LastFourDigits).HasConversion<LastFourDigitsConverter>().HasMaxLength(4).IsRequired();
        builder.Property(c => c.CreditLimit).IsRequired();
        builder.Property(c => c.ClosingDay).HasConversion<DayOfMonthConverter>().IsRequired();
        builder.Property(c => c.DueDay).HasConversion<DayOfMonthConverter>().IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t => t.HasCheckConstraint(
            "CK_Invoices_Dates", "\"StartDate\" < \"ClosingDate\" AND \"ClosingDate\" <= \"DueDate\""));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.ReferenceMonth).IsRequired();
        builder.Property(i => i.StartDate).IsRequired();
        builder.Property(i => i.ClosingDate).IsRequired();
        builder.Property(i => i.DueDate).IsRequired();
        builder.Ignore(i => i.Period);

        builder.HasOne<CreditCard>().WithMany().HasForeignKey(i => i.CreditCardId).OnDelete(DeleteBehavior.Restrict);

        // Uma fatura por cartão e mês: reimportar a mesma fatura nunca cria outra.
        builder.HasIndex(i => new { i.CreditCardId, i.ReferenceMonth }).IsUnique();
    }
}

internal sealed class InstallmentPurchaseConfiguration : IEntityTypeConfiguration<InstallmentPurchase>
{
    public void Configure(EntityTypeBuilder<InstallmentPurchase> builder)
    {
        builder.ToTable("InstallmentPurchases", t =>
        {
            t.HasCheckConstraint("CK_InstallmentPurchases_Count", "\"InstallmentCount\" BETWEEN 2 AND 48");
            t.HasCheckConstraint("CK_InstallmentPurchases_Amount", "CAST(\"InstallmentAmount\" AS REAL) > 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Description).HasMaxLength(InstallmentPurchase.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.MerchantKey).HasMaxLength(InstallmentPurchase.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.InstallmentAmount).IsRequired();
        builder.Property(p => p.InstallmentCount).IsRequired();
        builder.Property(p => p.TotalAmount).IsRequired();
        builder.Property(p => p.FirstInvoiceMonth).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasOne<CreditCard>().WithMany().HasForeignKey(p => p.CreditCardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.CreditCardId, p.MerchantKey });
    }
}
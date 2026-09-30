using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", t =>
        {
            t.HasCheckConstraint("CK_Transactions_Kind", CheckConstraints.EnumIn<TransactionKind>("Kind"));
            t.HasCheckConstraint(
                "CK_Transactions_Installment",
                "(\"InstallmentPurchaseId\" IS NULL AND \"InstallmentNumber\" IS NULL) OR (\"InstallmentPurchaseId\" IS NOT NULL AND \"InstallmentNumber\" BETWEEN 1 AND 48)");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Amount).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.MerchantName).HasMaxLength(Transaction.MerchantMaxLength).IsRequired();
        builder.Property(t => t.MerchantKey).HasMaxLength(Transaction.MerchantMaxLength).IsRequired();
        builder.Property(t => t.Kind).IsRequired();
        builder.Property(t => t.ExternalId).HasMaxLength(Transaction.ExternalIdMaxLength);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Ignore(t => t.SpendingAmount);

        builder.HasOne<CreditCard>().WithMany().HasForeignKey(t => t.CreditCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(t => t.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstallmentPurchase>().WithMany().HasForeignKey(t => t.InstallmentPurchaseId).OnDelete(DeleteBehavior.Restrict);

        // (CreditCardId, Date) atende filtros por cartão (prefixo à esquerda) e a busca de candidatos a duplicidade por período.
        builder.HasIndex(t => new { t.CreditCardId, t.Date });
        builder.HasIndex(t => t.InvoiceId);
        builder.HasIndex(t => t.Date);
        builder.HasIndex(t => t.ExternalId);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => t.ImportHash);
        builder.HasIndex(t => t.MerchantKey);
        builder.HasIndex(t => t.InstallmentPurchaseId);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal sealed class ImportConfiguration : IEntityTypeConfiguration<Import>
{
    public void Configure(EntityTypeBuilder<Import> builder)
    {
        builder.ToTable("Imports");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.FileName).HasMaxLength(Import.FileNameMaxLength).IsRequired();
        builder.Property(i => i.FileHash).IsRequired();
        builder.Property(i => i.FileType).IsRequired();
        builder.Property(i => i.ImportedAt).IsRequired();
        builder.Property(i => i.TransactionCount).IsRequired();
        builder.Property(i => i.Status).IsRequired();

        builder.HasOne<CreditCard>().WithMany().HasForeignKey(i => i.CreditCardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Transactions)
            .WithOne()
            .HasForeignKey(t => t.ImportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => i.FileHash);
    }
}

internal sealed class ImportTransactionConfiguration : IEntityTypeConfiguration<ImportTransaction>
{
    public void Configure(EntityTypeBuilder<ImportTransaction> builder)
    {
        builder.ToTable("ImportTransactions", t => t.HasCheckConstraint(
            "CK_ImportTransactions_Kind", $"\"Kind\" IS NULL OR {CheckConstraints.EnumIn<TransactionKind>("Kind")}"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.ExternalId).HasMaxLength(Transaction.ExternalIdMaxLength);
        builder.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength);
        builder.Property(t => t.ErrorMessage).HasMaxLength(ImportTransaction.ErrorMessageMaxLength);
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.DuplicateReason).IsRequired();

        // Aponta para o lançamento criado ou para o que causou a duplicidade.
        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(t => t.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.TransactionId);
    }
}
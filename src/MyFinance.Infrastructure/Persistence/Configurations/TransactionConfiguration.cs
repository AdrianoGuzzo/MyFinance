using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", t => t.HasCheckConstraint(
            "CK_Transactions_SingleOwner",
            "(\"AccountId\" IS NULL) <> (\"CreditCardId\" IS NULL)"));

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Amount).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.ExternalId).HasMaxLength(Transaction.ExternalIdMaxLength);
        builder.Property(t => t.TransactionType).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditCard>().WithMany().HasForeignKey(t => t.CreditCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);

        // (AccountId, Date) e (CreditCardId, Date) atendem filtros por conta/cartão (prefixo à esquerda)
        // e a busca de candidatos a duplicidade por período.
        builder.HasIndex(t => new { t.AccountId, t.Date });
        builder.HasIndex(t => new { t.CreditCardId, t.Date });
        builder.HasIndex(t => t.Date);
        builder.HasIndex(t => t.ExternalId);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => t.ImportHash);
    }
}
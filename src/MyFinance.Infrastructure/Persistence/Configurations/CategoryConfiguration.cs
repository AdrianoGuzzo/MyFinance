using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.ParentCategoryId);
    }
}

internal sealed class CategoryRuleConfiguration : IEntityTypeConfiguration<CategoryRule>
{
    public void Configure(EntityTypeBuilder<CategoryRule> builder)
    {
        builder.ToTable("CategoryRules", t => t.HasCheckConstraint(
            "CK_CategoryRules_Priority", $"\"Priority\" BETWEEN 0 AND {CategoryRule.MaxPriority}"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Pattern).HasMaxLength(CategoryRule.PatternMaxLength).IsRequired();
        builder.Property(r => r.Priority).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne<Category>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IsActive, r.Priority });
        builder.HasIndex(r => r.CategoryId);
    }
}

internal sealed class SpendingLimitConfiguration : IEntityTypeConfiguration<SpendingLimit>
{
    public void Configure(EntityTypeBuilder<SpendingLimit> builder)
    {
        builder.ToTable("SpendingLimits", t => t.HasCheckConstraint(
            "CK_SpendingLimits_Amount", "CAST(\"MonthlyAmount\" AS REAL) > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.MonthlyAmount).IsRequired();
        builder.Property(l => l.IsActive).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasOne<Category>().WithMany().HasForeignKey(l => l.CategoryId).OnDelete(DeleteBehavior.Restrict);

        // Um limite por categoria.
        builder.HasIndex(l => l.CategoryId).IsUnique();
    }
}

internal sealed class FinancialGoalConfiguration : IEntityTypeConfiguration<FinancialGoal>
{
    public void Configure(EntityTypeBuilder<FinancialGoal> builder)
    {
        builder.ToTable("FinancialGoals", t => t.HasCheckConstraint(
            "CK_FinancialGoals_Target", "CAST(\"MonthlyTarget\" AS REAL) > 0"));
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Name).HasMaxLength(FinancialGoal.NameMaxLength).IsRequired();
        builder.Property(g => g.MonthlyTarget).IsRequired();
        builder.Property(g => g.StartMonth).IsRequired();
        builder.Property(g => g.IsActive).IsRequired();
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Ignore(g => g.AnnualTarget);
    }
}

internal sealed class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("RecurringExpenses", t => t.HasCheckConstraint(
            "CK_RecurringExpenses_Classification", CheckConstraints.EnumIn<RecurringClassification>("Classification")));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.MerchantKey).HasMaxLength(RecurringExpense.NameMaxLength).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(RecurringExpense.NameMaxLength).IsRequired();
        builder.Property(r => r.EstimatedMonthlyAmount).IsRequired();
        builder.Property(r => r.Classification).IsRequired();
        builder.Property(r => r.IsDismissed).IsRequired();
        builder.Property(r => r.LastSeenMonth).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
        builder.Ignore(r => r.EstimatedAnnualAmount);

        builder.HasOne<Category>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.MerchantKey).IsUnique();
    }
}
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasOne(category => category.User)
            .WithMany(user => user.Categories)
            .HasForeignKey(category => category.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(category => category.ParentCategory)
            .WithMany(category => category.SubCategories)
            .HasForeignKey(category => category.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => new { category.UserId, category.Name, category.ParentCategoryId })
            .IsUnique()
            .HasFilter("parent_category_id IS NOT NULL");

        builder.HasIndex(category => new { category.UserId, category.Name })
            .IsUnique()
            .HasFilter("parent_category_id IS NULL");
    }
}

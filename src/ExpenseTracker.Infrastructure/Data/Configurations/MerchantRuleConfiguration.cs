using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class MerchantRuleConfiguration : IEntityTypeConfiguration<MerchantRule>
{
    public void Configure(EntityTypeBuilder<MerchantRule> builder)
    {
        builder.ToTable("merchant_rules");

        builder.HasOne(rule => rule.User)
            .WithMany(user => user.MerchantRules)
            .HasForeignKey(rule => rule.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rule => new { rule.UserId, rule.MerchantNormalized })
            .IsUnique();

        builder.HasOne(rule => rule.Category)
            .WithMany()
            .HasForeignKey(rule => rule.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

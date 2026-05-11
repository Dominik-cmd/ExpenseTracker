using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class InvestmentProviderConfiguration : IEntityTypeConfiguration<InvestmentProvider>
{
    public void Configure(EntityTypeBuilder<InvestmentProvider> builder)
    {
        builder.ToTable("investment_providers");

        builder.HasOne(p => p.User)
            .WithMany(u => u.InvestmentProviders)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.ProviderType).HasConversion<string>();
        builder.Property(p => p.ExtraConfig).HasColumnType("jsonb");

        builder.HasIndex(p => new { p.UserId, p.ProviderType }).IsUnique();
    }
}

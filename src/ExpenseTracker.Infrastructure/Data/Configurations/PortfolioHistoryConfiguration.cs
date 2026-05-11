using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class PortfolioHistoryConfiguration : IEntityTypeConfiguration<PortfolioHistory>
{
    public void Configure(EntityTypeBuilder<PortfolioHistory> builder)
    {
        builder.ToTable("portfolio_history");

        builder.HasOne(h => h.Account)
            .WithMany(a => a.PortfolioHistory)
            .HasForeignKey(h => h.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(h => h.MarketValue).HasPrecision(18, 4);

        builder.HasIndex(h => new { h.AccountId, h.SnapshotDate }).IsUnique();
    }
}

using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class ManualBalanceHistoryConfiguration : IEntityTypeConfiguration<ManualBalanceHistory>
{
    public void Configure(EntityTypeBuilder<ManualBalanceHistory> builder)
    {
        builder.ToTable("manual_balance_history");

        builder.HasOne(h => h.Account)
            .WithMany(a => a.BalanceHistory)
            .HasForeignKey(h => h.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(h => h.Balance).HasPrecision(18, 4);

        builder.HasIndex(h => new { h.AccountId, h.RecordedAt })
            .IsDescending(false, true);
    }
}

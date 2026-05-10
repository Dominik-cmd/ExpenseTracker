using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings");

        builder.HasOne(h => h.Account)
            .WithMany(a => a.Holdings)
            .HasForeignKey(h => h.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Instrument)
            .WithMany(i => i.Holdings)
            .HasForeignKey(h => h.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(h => h.Quantity).HasPrecision(18, 4);
        builder.Property(h => h.CostBasisPerShare).HasPrecision(18, 4);
        builder.Property(h => h.MarkPrice).HasPrecision(18, 4);
        builder.Property(h => h.MarketValue).HasPrecision(18, 4);
        builder.Property(h => h.UnrealizedPnl).HasPrecision(18, 4);
        builder.Property(h => h.UnrealizedPnlPercent).HasPrecision(10, 4);

        builder.HasIndex(h => new { h.AccountId, h.InstrumentId }).IsUnique();
    }
}

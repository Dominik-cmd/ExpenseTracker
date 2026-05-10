using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class InvestmentTransactionConfiguration : IEntityTypeConfiguration<InvestmentTransaction>
{
    public void Configure(EntityTypeBuilder<InvestmentTransaction> builder)
    {
        builder.ToTable("investment_transactions");

        builder.HasOne(t => t.Account)
            .WithMany(a => a.InvestmentTransactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Instrument)
            .WithMany(i => i.InvestmentTransactions)
            .HasForeignKey(t => t.InstrumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.Quantity).HasPrecision(18, 4);
        builder.Property(t => t.Price).HasPrecision(18, 4);
        builder.Property(t => t.GrossAmount).HasPrecision(18, 4);
        builder.Property(t => t.Commission).HasPrecision(18, 4);
        builder.Property(t => t.NetAmount).HasPrecision(18, 4);

        builder.HasIndex(t => new { t.AccountId, t.ExternalTransactionId }).IsUnique();
        builder.HasIndex(t => t.TransactionDate);
    }
}

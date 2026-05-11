using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class ManualAccountBalanceConfiguration : IEntityTypeConfiguration<ManualAccountBalance>
{
    public void Configure(EntityTypeBuilder<ManualAccountBalance> builder)
    {
        builder.ToTable("manual_account_balances");

        builder.HasOne(b => b.Account)
            .WithOne(a => a.ManualBalance)
            .HasForeignKey<ManualAccountBalance>(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(b => b.Balance).HasPrecision(18, 4);

        builder.HasIndex(b => b.AccountId).IsUnique();
    }
}

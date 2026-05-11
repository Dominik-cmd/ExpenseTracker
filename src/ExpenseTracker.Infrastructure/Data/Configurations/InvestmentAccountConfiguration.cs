using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class InvestmentAccountConfiguration : IEntityTypeConfiguration<InvestmentAccount>
{
    public void Configure(EntityTypeBuilder<InvestmentAccount> builder)
    {
        builder.ToTable("investment_accounts");

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Accounts)
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany(u => u.InvestmentAccounts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.AccountType).HasConversion<string>();
    }
}

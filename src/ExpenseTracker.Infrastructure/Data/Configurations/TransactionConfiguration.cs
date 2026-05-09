using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(18, 2);

        builder.HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(transaction => transaction.RawMessage)
            .WithMany(rawMessage => rawMessage.Transactions)
            .HasForeignKey(transaction => transaction.RawMessageId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(transaction => transaction.User)
            .WithMany(user => user.Transactions)
            .HasForeignKey(transaction => transaction.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(transaction => transaction.TransactionDate);
        builder.HasIndex(transaction => transaction.CategoryId);
        builder.HasIndex(transaction => transaction.IsDeleted);
        builder.HasIndex(transaction => new { transaction.UserId, transaction.TransactionDate, transaction.IsDeleted });
    }
}

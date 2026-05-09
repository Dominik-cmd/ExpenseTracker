using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class RawMessageConfiguration : IEntityTypeConfiguration<RawMessage>
{
    public void Configure(EntityTypeBuilder<RawMessage> builder)
    {
        builder.ToTable("raw_messages");

        builder.HasOne(rawMessage => rawMessage.User)
            .WithMany(user => user.RawMessages)
            .HasForeignKey(rawMessage => rawMessage.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rawMessage => rawMessage.IdempotencyHash)
            .IsUnique();

        builder.HasIndex(rawMessage => rawMessage.ParseStatus);
        builder.HasIndex(rawMessage => rawMessage.UserId);
    }
}

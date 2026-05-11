using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(user => user.Username)
            .IsUnique();

        builder.Property(user => user.CreatedAt)
            .HasDefaultValueSql("TIMEZONE('utc', now())");

        builder.Property(user => user.UpdatedAt)
            .HasDefaultValueSql("TIMEZONE('utc', now())");
    }
}

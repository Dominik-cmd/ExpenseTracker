using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Data.Configurations;

public sealed class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
    public void Configure(EntityTypeBuilder<Summary> builder)
    {
        builder.ToTable("summaries");
        builder.HasKey(summary => summary.Id);

        builder.Property(summary => summary.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(summary => summary.GeneratedAt)
            .HasDefaultValueSql("TIMEZONE('utc', now())");

        builder.HasIndex(summary => new { summary.UserId, summary.SummaryType, summary.Scope, summary.CacheKey })
            .HasDatabaseName("ix_summaries_lookup")
            .IsUnique();
        builder.HasIndex(summary => new { summary.UserId, summary.SummaryType, summary.Scope, summary.GeneratedAt })
            .HasDatabaseName("ix_summaries_scope");
        builder.HasIndex(summary => summary.UserId)
            .HasDatabaseName("ix_summaries_user_id");
    }
}

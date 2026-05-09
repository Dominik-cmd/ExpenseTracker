using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class LlmProviderConfiguration : IEntityTypeConfiguration<LlmProvider>
{
    public void Configure(EntityTypeBuilder<LlmProvider> builder)
    {
        builder.ToTable("llm_providers");

        builder.Property(provider => provider.ProviderType)
            .HasConversion<string>();

        builder.HasIndex(provider => provider.IsEnabled)
            .IsUnique()
            .HasFilter("is_enabled = true");
    }
}

using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Data.Configurations;

internal sealed class LlmCallLogConfiguration : IEntityTypeConfiguration<LlmCallLog>
{
    public void Configure(EntityTypeBuilder<LlmCallLog> builder)
    {
        builder.ToTable("llm_call_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProviderType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SystemPrompt).IsRequired();
        builder.Property(x => x.UserPrompt).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(100).HasDefaultValue("categorize").IsRequired();
        builder.Property(x => x.MerchantRaw).HasMaxLength(500);
        builder.Property(x => x.MerchantNormalized).HasMaxLength(500);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.ParsedCategory).HasMaxLength(200);
        builder.Property(x => x.ParsedSubcategory).HasMaxLength(200);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ProviderType);
        builder.HasIndex(x => x.UserId);
    }
}

using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments");

        builder.HasIndex(i => new { i.Symbol, i.AssetClass, i.Currency }).IsUnique();
    }
}

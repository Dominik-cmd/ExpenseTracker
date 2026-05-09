using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure;

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(setting => new { setting.Key, setting.UserId });

        builder.HasOne(setting => setting.User)
            .WithMany(user => user.Settings)
            .HasForeignKey(setting => setting.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

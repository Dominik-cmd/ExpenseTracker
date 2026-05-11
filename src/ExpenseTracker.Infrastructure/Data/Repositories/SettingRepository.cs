using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class SettingRepository(AppDbContext dbContext) : ISettingRepository
{
    public async Task<Setting?> GetAsync(Guid userId, string key, CancellationToken ct)
    {
        return await dbContext.Settings
          .FirstOrDefaultAsync(x => x.UserId == userId && x.Key == key, ct);
    }

    public async Task<List<Setting>> GetAllByKeyAsync(string key, CancellationToken ct)
    {
        return await dbContext.Settings
          .Where(x => x.Key == key)
          .AsNoTracking()
          .ToListAsync(ct);
    }

    public async Task UpsertAsync(Guid userId, string key, string? value, CancellationToken ct)
    {
        var setting = await dbContext.Settings
          .FirstOrDefaultAsync(x => x.UserId == userId && x.Key == key, ct);

        if (setting is null)
        {
            setting = new Setting
            {
                UserId = userId,
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await dbContext.Settings.AddAsync(setting, ct);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}

using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface ISettingRepository
{
  Task<Setting?> GetAsync(Guid userId, string key, CancellationToken ct);
  Task<List<Setting>> GetAllByKeyAsync(string key, CancellationToken ct);
  Task UpsertAsync(Guid userId, string key, string? value, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}

namespace ExpenseTracker.Application.Interfaces;

public interface ILlmLogService
{
    Task<object> GetLogsAsync(
      Guid userId, int page, int pageSize,
      string? provider, string? purpose, bool? successOnly,
      CancellationToken ct);
    Task DeleteAllAsync(Guid userId, CancellationToken ct);
}

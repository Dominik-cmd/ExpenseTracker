using ExpenseTracker.Application.Interfaces;

namespace ExpenseTracker.Application.Services;

public sealed class LlmLogService(ILlmCallLogRepository llmCallLogRepository) : ILlmLogService
{
    public async Task<object> GetLogsAsync(
      Guid userId, int page, int pageSize,
      string? provider, string? purpose, bool? successOnly,
      CancellationToken ct)
    {
        var (items, totalCount) = await llmCallLogRepository.GetPagedAsync(
          userId, provider, purpose, successOnly, page, pageSize, ct);

        return new
        {
            items = items.Select(l => new
            {
                l.Id,
                l.ProviderType,
                l.Model,
                l.Purpose,
                l.Success,
                l.LatencyMs,
                l.CreatedAt,
                userPrompt = l.UserPrompt?.Length > 200
                ? l.UserPrompt[..200] + "..."
                : l.UserPrompt,
                responseRaw = l.ResponseRaw?.Length > 200
                ? l.ResponseRaw[..200] + "..."
                : l.ResponseRaw
            }),
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken ct)
    {
        await llmCallLogRepository.DeleteAllForUserAsync(userId, ct);
        await llmCallLogRepository.SaveChangesAsync(ct);
    }
}

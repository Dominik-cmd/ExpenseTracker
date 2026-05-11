using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Interfaces;

public interface IRawMessageService
{
    Task<QueueStatusDto> GetQueueStatusAsync(Guid userId, CancellationToken ct);
    Task<List<RawMessageDto>> GetAllAsync(Guid userId, ParseStatus? status, CancellationToken ct);
    Task<bool> ReprocessAsync(Guid userId, Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

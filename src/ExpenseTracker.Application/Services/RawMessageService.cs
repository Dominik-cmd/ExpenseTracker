using System.Threading.Channels;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Services;

public sealed class RawMessageService(
  IRawMessageRepository rawMessageRepository,
  Channel<Guid> smsChannel) : IRawMessageService
{
    public async Task<QueueStatusDto> GetQueueStatusAsync(Guid userId, CancellationToken ct)
    {
        var pendingCount = await rawMessageRepository.GetPendingCountAsync(userId, ct);
        var pendingMessages = await rawMessageRepository.GetByStatusAsync(userId, ParseStatus.Pending, ct);
        var recentProcessed = await rawMessageRepository.GetRecentProcessedAsync(userId, 20, ct);

        var pending = pendingMessages
          .Select(m => new QueuedItemDto(
            m.Id,
            m.Body.Length > 80 ? m.Body[..80] + "..." : m.Body,
            m.CreatedAt))
          .ToList();

        var recent = recentProcessed
          .Select(m => new RecentItemDto(
            m.Id,
            m.Body.Length > 80 ? m.Body[..80] + "..." : m.Body,
            m.ParseStatus.ToString(),
            m.ErrorMessage,
            m.UpdatedAt))
          .ToList();

        return new QueueStatusDto(pendingCount, pending, recent);
    }

    public async Task<List<RawMessageDto>> GetAllAsync(
      Guid userId, ParseStatus? status, CancellationToken ct)
    {
        var messages = await rawMessageRepository.GetByStatusAsync(userId, status, ct);

        return messages
          .Select(m => new RawMessageDto(
            m.Id, m.Sender, m.Body, m.ReceivedAt,
            m.ParseStatus, m.ErrorMessage, m.IdempotencyHash,
            m.TransactionId, m.CreatedAt))
          .ToList();
    }

    public async Task<bool> ReprocessAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var message = await rawMessageRepository.GetByIdAsync(id, userId, ct);
        if (message is null)
        {
            return false;
        }

        message.ParseStatus = ParseStatus.Pending;
        message.ErrorMessage = null;
        message.UpdatedAt = DateTime.UtcNow;
        await rawMessageRepository.SaveChangesAsync(ct);
        await smsChannel.Writer.WriteAsync(message.Id, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var message = await rawMessageRepository.GetByIdAsync(id, userId, ct);
        if (message is null)
        {
            return false;
        }

        await rawMessageRepository.RemoveAsync(message, ct);
        await rawMessageRepository.SaveChangesAsync(ct);
        return true;
    }
}

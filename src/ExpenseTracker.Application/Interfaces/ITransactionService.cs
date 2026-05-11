using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface ITransactionService
{
  Task<PagedResult<TransactionDto>> GetPagedAsync(Guid userId, TransactionFilterParams filter, CancellationToken ct);
  Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
  Task<TransactionDto> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken ct);
  Task<TransactionDto?> UpdateAsync(Guid id, Guid userId, UpdateTransactionRequest request, CancellationToken ct);
  Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
  Task<TransactionDto?> RecategorizeAsync(Guid id, Guid userId, RecategorizeRequest request, CancellationToken ct);
  Task BulkRecategorizeAsync(Guid userId, BulkRecategorizeRequest request, CancellationToken ct);
  Task<byte[]> ExportCsvAsync(Guid userId, TransactionFilterParams filter, CancellationToken ct);
}

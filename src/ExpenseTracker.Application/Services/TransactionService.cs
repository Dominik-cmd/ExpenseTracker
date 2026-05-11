using System.Globalization;
using System.Text;
using CsvHelper;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Services;

public sealed class TransactionService(
  ITransactionRepository transactionRepository,
  ICategoryRepository categoryRepository,
  IMerchantRuleRepository merchantRuleRepository,
  IAuditLogRepository auditLogRepository,
  ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<PagedResult<TransactionDto>> GetPagedAsync(
      Guid userId, TransactionFilterParams filter, CancellationToken ct)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var repoFilter = new TransactionFilter(
          filter.From, filter.To, filter.CategoryId, filter.CategoryIds,
          filter.Merchant, filter.MinAmount, filter.MaxAmount,
          filter.Direction, filter.Source);

        var (items, totalCount) = await transactionRepository.GetPagedAsync(
          userId, repoFilter, page, pageSize, ct);

        return new PagedResult<TransactionDto>(
          items.Select(x => x.ToDto()).ToList(),
          totalCount, page, pageSize);
    }

    public async Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var transaction = await transactionRepository.GetByIdAsync(id, userId, ct);
        return transaction?.ToDto();
    }

    public async Task<TransactionDto> CreateAsync(
      Guid userId, CreateTransactionRequest request, CancellationToken ct)
    {
        if (!await categoryRepository.ExistsAsync(request.CategoryId, userId, ct))
        {
            throw new InvalidOperationException("Category does not exist.");
        }

        var transaction = new Transaction
        {
            UserId = userId,
            Amount = request.Amount,
            Currency = "EUR",
            Direction = request.Direction,
            TransactionType = request.TransactionType,
            TransactionDate = request.TransactionDate,
            MerchantRaw = request.MerchantRaw ?? string.Empty,
            MerchantNormalized = MerchantNormalizer.Normalize(request.MerchantRaw ?? string.Empty),
            CategoryId = request.CategoryId,
            CategorySource = CategorySource.Manual,
            TransactionSource = TransactionSource.Manual,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await transactionRepository.AddAsync(transaction, ct);
        await transactionRepository.SaveChangesAsync(ct);

        logger.LogDebug("Transaction {TransactionId} created for user {UserId}, merchant {Merchant}, amount {Amount}",
          transaction.Id, userId, transaction.MerchantNormalized, transaction.Amount);

        var created = await transactionRepository.GetByIdAsync(transaction.Id, userId, ct);
        return created!.ToDto();
    }

    public async Task<TransactionDto?> UpdateAsync(
      Guid id, Guid userId, UpdateTransactionRequest request, CancellationToken ct)
    {
        var transaction = await transactionRepository.GetByIdAsync(id, userId, ct);
        if (transaction is null)
        {
            return null;
        }

        var changes = new Dictionary<string, object?>();

        if (request.Amount.HasValue)
        {
            transaction.Amount = request.Amount.Value;
            changes[nameof(transaction.Amount)] = request.Amount.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            transaction.Currency = request.Currency.Trim().ToUpperInvariant();
            changes[nameof(transaction.Currency)] = transaction.Currency;
        }

        if (request.Direction.HasValue)
        {
            transaction.Direction = request.Direction.Value;
            changes[nameof(transaction.Direction)] = request.Direction.Value;
        }

        if (request.TransactionType.HasValue)
        {
            transaction.TransactionType = request.TransactionType.Value;
            changes[nameof(transaction.TransactionType)] = request.TransactionType.Value;
        }

        if (request.TransactionDate.HasValue)
        {
            transaction.TransactionDate = request.TransactionDate.Value;
            changes[nameof(transaction.TransactionDate)] = request.TransactionDate.Value;
        }

        if (request.MerchantRaw is not null)
        {
            transaction.MerchantRaw = request.MerchantRaw;
            transaction.MerchantNormalized = MerchantNormalizer.Normalize(request.MerchantRaw);
            changes[nameof(transaction.MerchantRaw)] = request.MerchantRaw;
        }

        if (request.CategoryId.HasValue)
        {
            if (!await categoryRepository.ExistsAsync(request.CategoryId.Value, userId, ct))
            {
                throw new InvalidOperationException("Category does not exist.");
            }

            transaction.CategoryId = request.CategoryId.Value;
            transaction.CategorySource = CategorySource.Manual;
            changes[nameof(transaction.CategoryId)] = request.CategoryId.Value;
        }

        if (request.Notes is not null)
        {
            transaction.Notes = request.Notes;
            changes[nameof(transaction.Notes)] = request.Notes;
        }

        transaction.UpdatedAt = DateTime.UtcNow;

        await auditLogRepository.AddAsync(new AuditLog
        {
            EntityType = nameof(Transaction),
            EntityId = transaction.Id.ToString(),
            Action = "Patch",
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        }, ct);

        await transactionRepository.SaveChangesAsync(ct);

        var updated = await transactionRepository.GetByIdAsync(transaction.Id, userId, ct);
        return updated!.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var transaction = await transactionRepository.GetByIdAsync(id, userId, ct);
        if (transaction is null)
        {
            return false;
        }

        transaction.IsDeleted = true;
        transaction.UpdatedAt = DateTime.UtcNow;
        await transactionRepository.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TransactionDto?> RecategorizeAsync(
      Guid id, Guid userId, RecategorizeRequest request, CancellationToken ct)
    {
        var transaction = await transactionRepository.GetByIdAsync(id, userId, ct);
        if (transaction is null)
        {
            return null;
        }

        if (!await categoryRepository.ExistsAsync(request.CategoryId, userId, ct))
        {
            throw new InvalidOperationException("Category does not exist.");
        }

        transaction.CategoryId = request.CategoryId;
        transaction.CategorySource = CategorySource.Manual;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (request.CreateMerchantRule && !string.IsNullOrWhiteSpace(transaction.MerchantNormalized))
        {
            await UpsertMerchantRuleAsync(userId, transaction.MerchantNormalized, request.CategoryId, "manual", ct);
        }

        await transactionRepository.SaveChangesAsync(ct);

        var updated = await transactionRepository.GetByIdAsync(transaction.Id, userId, ct);
        return updated!.ToDto();
    }

    public async Task BulkRecategorizeAsync(
      Guid userId, BulkRecategorizeRequest request, CancellationToken ct)
    {
        if (!await categoryRepository.ExistsAsync(request.CategoryId, userId, ct))
        {
            throw new InvalidOperationException("Category does not exist.");
        }

        var transactions = await transactionRepository.GetByIdsAsync(request.TransactionIds, userId, ct);

        foreach (var transaction in transactions)
        {
            transaction.CategoryId = request.CategoryId;
            transaction.CategorySource = CategorySource.Manual;
            transaction.UpdatedAt = DateTime.UtcNow;

            if (request.CreateMerchantRule && !string.IsNullOrWhiteSpace(transaction.MerchantNormalized))
            {
                await UpsertMerchantRuleAsync(userId, transaction.MerchantNormalized, request.CategoryId, "manual", ct);
            }
        }

        await transactionRepository.SaveChangesAsync(ct);
        logger.LogInformation("Bulk recategorized {Count} transactions to category {CategoryId} for user {UserId}",
          transactions.Count, request.CategoryId, userId);
    }

    public async Task<byte[]> ExportCsvAsync(
      Guid userId, TransactionFilterParams filter, CancellationToken ct)
    {
        var repoFilter = new TransactionFilter(
          filter.From, filter.To, filter.CategoryId, filter.CategoryIds,
          filter.Merchant, filter.MinAmount, filter.MaxAmount,
          filter.Direction, filter.Source);

        var items = await transactionRepository.GetAllAsync(userId, repoFilter, ct);

        await using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(items.Select(x => x.ToDto()), ct);
        await writer.FlushAsync(ct);
        return stream.ToArray();
    }

    private async Task UpsertMerchantRuleAsync(
      Guid userId, string merchantNormalized, Guid categoryId, string createdBy, CancellationToken ct)
    {
        var rule = await merchantRuleRepository.GetByMerchantAsync(userId, merchantNormalized, ct);
        if (rule is null)
        {
            await merchantRuleRepository.AddAsync(new MerchantRule
            {
                UserId = userId,
                MerchantNormalized = merchantNormalized,
                CategoryId = categoryId,
                CreatedBy = createdBy,
                HitCount = 0,
                LastHitAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, ct);
            return;
        }

        rule.CategoryId = categoryId;
        rule.LastHitAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
    }
}

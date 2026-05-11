using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class InvestmentService(
  InvestmentAnalyticsService analyticsService,
  NarrativeService narrativeService,
  IInvestmentRepository investmentRepository,
  AppDbContext dbContext,
  IbkrFlexProvider ibkrFlexProvider,
  IbkrPersistenceService ibkrPersistenceService,
  PortfolioHistoryService portfolioHistoryService,
  ILogger<InvestmentService> logger) : IInvestmentService
{
    public Task<PortfolioSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct)
      => analyticsService.GetSummaryAsync(userId, ct);

    public Task<List<AccountSummaryDto>> GetAccountsAsync(Guid userId, CancellationToken ct)
      => analyticsService.GetAccountsAsync(userId, ct);

    public Task<List<HoldingDto>> GetHoldingsAsync(Guid userId, CancellationToken ct)
      => analyticsService.GetHoldingsAsync(userId, ct);

    public Task<AllocationBreakdownDto> GetAllocationAsync(Guid userId, string type, CancellationToken ct)
      => analyticsService.GetAllocationAsync(userId, type, ct);

    public Task<List<HistoryPointDto>> GetHistoryAsync(
      Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return analyticsService.GetHistoryAsync(userId, fromDate, toDate, ct);
    }

    public Task<List<RecentActivityDto>> GetActivityAsync(Guid userId, int limit, CancellationToken ct)
      => analyticsService.GetRecentActivityAsync(userId, limit, ct);

    public Task<DashboardStripDto> GetDashboardStripAsync(Guid userId, CancellationToken ct)
      => analyticsService.GetDashboardStripAsync(userId, ct);

    public async Task<object?> GetNarrativeAsync(Guid userId, CancellationToken ct)
      => await narrativeService.GetInvestmentNarrativeAsync(userId, ct);

    public async Task<object> TriggerSyncAsync(Guid userId, CancellationToken ct)
    {
        var ibkrProvider = await dbContext.InvestmentProviders
          .FirstOrDefaultAsync(p => p.UserId == userId
            && p.ProviderType == InvestmentProviderType.Ibkr
            && p.IsEnabled, ct);

        if (ibkrProvider is null)
        {
            return new { status = "no_provider", message = "No enabled IBKR provider found." };
        }

        try
        {
            var result = await ibkrFlexProvider.SyncAsync(ibkrProvider.Id, ct);
            await ibkrPersistenceService.PersistAsync(ibkrProvider.Id, userId, result, ct);

            ibkrProvider.LastSyncAt = DateTime.UtcNow;
            ibkrProvider.LastSyncStatus = "success";
            ibkrProvider.LastSyncError = null;
            await dbContext.SaveChangesAsync(ct);

            var accounts = await dbContext.InvestmentAccounts
              .Where(a => a.ProviderId == ibkrProvider.Id && a.IsActive)
              .Select(a => a.Id)
              .ToListAsync(ct);

            foreach (var accountId in accounts)
            {
                await portfolioHistoryService.SnapshotAccountAsync(accountId, ct);
            }

            return new { status = "success", accountsSynced = accounts.Count };
        }
        catch (Exception ex)
        {
            ibkrProvider.LastSyncAt = DateTime.UtcNow;
            ibkrProvider.LastSyncStatus = "failure";
            ibkrProvider.LastSyncError = ex.Message;
            await dbContext.SaveChangesAsync(ct);
            logger.LogError(ex, "Manual sync failed for user {UserId}.", userId);
            return new { status = "failure", error = ex.Message };
        }
    }

    public async Task<object> GetSyncStatusAsync(Guid userId, CancellationToken ct)
    {
        var provider = await dbContext.InvestmentProviders
          .AsNoTracking()
          .FirstOrDefaultAsync(p => p.UserId == userId
            && p.ProviderType == InvestmentProviderType.Ibkr, ct);

        if (provider is null)
        {
            return new { configured = false };
        }

        return new
        {
            configured = true,
            isEnabled = provider.IsEnabled,
            lastSyncAt = provider.LastSyncAt,
            lastSyncStatus = provider.LastSyncStatus,
            lastSyncError = provider.LastSyncError
        };
    }

    public async Task<List<ManualAccountDto>> GetManualAccountsAsync(Guid userId, CancellationToken ct)
    {
        var provider = await dbContext.InvestmentProviders
          .AsNoTracking()
          .FirstOrDefaultAsync(p => p.UserId == userId
            && p.ProviderType == InvestmentProviderType.Manual, ct);

        if (provider is null)
        {
            return [];
        }

        var accounts = await dbContext.InvestmentAccounts
          .AsNoTracking()
          .Include(a => a.ManualBalance)
          .Where(a => a.ProviderId == provider.Id && a.IsActive)
          .OrderBy(a => a.SortOrder)
          .ToListAsync(ct);

        return accounts.Select(a => new ManualAccountDto(
          a.Id, a.DisplayName, a.AccountType.ToString(), a.BaseCurrency,
          a.ManualBalance?.Balance, a.Icon, a.Color, a.Notes,
          a.IsActive, a.ManualBalance?.UpdatedAt)).ToList();
    }

    public async Task<object> CreateManualAccountAsync(
      Guid userId, CreateManualAccountRequest request, CancellationToken ct)
    {
        var provider = await dbContext.InvestmentProviders
          .FirstOrDefaultAsync(p => p.UserId == userId
            && p.ProviderType == InvestmentProviderType.Manual, ct);

        if (provider is null)
        {
            throw new InvalidOperationException("Manual investment provider not found.");
        }

        if (!provider.IsEnabled)
        {
            provider.IsEnabled = true;
        }

        if (!Enum.TryParse<AccountType>(request.AccountType, true, out var accountType))
        {
            accountType = AccountType.Other;
        }

        var account = new InvestmentAccount
        {
            ProviderId = provider.Id,
            UserId = userId,
            DisplayName = request.DisplayName,
            AccountType = accountType,
            BaseCurrency = request.Currency ?? "EUR",
            Icon = request.Icon,
            Color = request.Color,
            Notes = request.Notes,
            IsActive = true
        };

        dbContext.InvestmentAccounts.Add(account);

        if (request.InitialBalance.HasValue)
        {
            dbContext.Set<ManualAccountBalance>().Add(new ManualAccountBalance
            {
                AccountId = account.Id,
                Balance = request.InitialBalance.Value,
                Currency = request.Currency ?? "EUR"
            });

            dbContext.Set<ManualBalanceHistory>().Add(new ManualBalanceHistory
            {
                AccountId = account.Id,
                Balance = request.InitialBalance.Value,
                Currency = request.Currency ?? "EUR",
                Note = "Initial balance"
            });
        }

        await dbContext.SaveChangesAsync(ct);

        return new
        {
            id = account.Id,
            displayName = account.DisplayName,
            accountType = account.AccountType.ToString(),
            currency = account.BaseCurrency,
            balance = request.InitialBalance
        };
    }

    public async Task<bool> UpdateManualAccountAsync(
      Guid userId, Guid id, UpdateManualAccountRequest request, CancellationToken ct)
    {
        var account = await dbContext.InvestmentAccounts
          .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (account is null)
        {
            return false;
        }

        if (request.DisplayName is not null)
        {
            account.DisplayName = request.DisplayName;
        }

        if (request.AccountType is not null && Enum.TryParse<AccountType>(request.AccountType, true, out var at))
        {
            account.AccountType = at;
        }

        if (request.Icon is not null)
        {
            account.Icon = request.Icon;
        }

        if (request.Color is not null)
        {
            account.Color = request.Color;
        }

        if (request.Notes is not null)
        {
            account.Notes = request.Notes;
        }

        if (request.IsActive.HasValue)
        {
            account.IsActive = request.IsActive.Value;
        }

        account.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteManualAccountAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var account = await dbContext.InvestmentAccounts
          .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (account is null)
        {
            return false;
        }

        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<object?> UpdateBalanceAsync(
      Guid userId, Guid id, UpdateBalanceRequest request, CancellationToken ct)
    {
        var account = await dbContext.InvestmentAccounts
          .Include(a => a.ManualBalance)
          .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (account is null)
        {
            return null;
        }

        if (account.ManualBalance is null)
        {
            dbContext.Set<ManualAccountBalance>().Add(new ManualAccountBalance
            {
                AccountId = account.Id,
                Balance = request.NewBalance,
                Currency = account.BaseCurrency
            });
        }
        else
        {
            account.ManualBalance.Balance = request.NewBalance;
            account.ManualBalance.UpdatedAt = DateTime.UtcNow;
        }

        dbContext.Set<ManualBalanceHistory>().Add(new ManualBalanceHistory
        {
            AccountId = account.Id,
            Balance = request.NewBalance,
            Currency = account.BaseCurrency,
            Note = request.Note
        });

        await dbContext.SaveChangesAsync(ct);
        await portfolioHistoryService.SnapshotAccountAsync(account.Id, ct);

        return new
        {
            accountId = account.Id,
            balance = request.NewBalance,
            updatedAt = DateTime.UtcNow
        };
    }

    public async Task<object?> GetBalanceHistoryAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var account = await dbContext.InvestmentAccounts
          .AsNoTracking()
          .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (account is null)
        {
            return null;
        }

        var history = await investmentRepository.GetBalanceHistoryAsync(id, ct);

        return history.Select(h => new
        {
            h.Id,
            h.Balance,
            h.Currency,
            h.RecordedAt,
            h.Note
        }).ToList();
    }
}

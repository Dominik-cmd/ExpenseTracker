using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/investments")]
public sealed class InvestmentsController(
    AppDbContext dbContext,
    InvestmentAnalyticsService analyticsService,
    PortfolioHistoryService historyService,
    IbkrFlexProvider ibkrProvider,
    IbkrPersistenceService ibkrPersistence,
    NarrativeService narrativeService,
    ILogger<InvestmentsController> logger) : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PortfolioSummaryDto>> GetSummary(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var summary = await analyticsService.GetSummaryAsync(userId.Value, ct);
        return Ok(summary);
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<List<AccountSummaryDto>>> GetAccounts(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var accounts = await analyticsService.GetAccountsAsync(userId.Value, ct);
        return Ok(accounts);
    }

    [HttpGet("holdings")]
    public async Task<ActionResult<List<HoldingDto>>> GetHoldings(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var holdings = await analyticsService.GetHoldingsAsync(userId.Value, ct);
        return Ok(holdings);
    }

    [HttpGet("allocation")]
    public async Task<ActionResult<AllocationBreakdownDto>> GetAllocation([FromQuery] string type = "accountType", CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var allocation = await analyticsService.GetAllocationAsync(userId.Value, type, ct);
        return Ok(allocation);
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<HistoryPointDto>>> GetHistory(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var history = await analyticsService.GetHistoryAsync(userId.Value, fromDate, toDate, ct);
        return Ok(history);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<List<RecentActivityDto>>> GetActivity([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var activity = await analyticsService.GetRecentActivityAsync(userId.Value, limit, ct);
        return Ok(activity);
    }

    [HttpGet("dashboard-strip")]
    public async Task<ActionResult<DashboardStripDto>> GetDashboardStrip(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var strip = await analyticsService.GetDashboardStripAsync(userId.Value, ct);
        return Ok(strip);
    }

    [HttpGet("narrative")]
    public async Task<IActionResult> GetNarrative(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var narrative = await narrativeService.GetInvestmentNarrativeAsync(userId.Value, ct);
        return narrative is not null ? Ok(narrative) : Ok(new { content = (string?)null });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> TriggerSync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var provider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.ProviderType == InvestmentProviderType.Ibkr && p.IsEnabled, ct);

        if (provider is null)
            return BadRequest(new { error = "IBKR provider not configured or not enabled" });

        try
        {
            var result = await ibkrProvider.SyncAsync(provider.Id, ct);
            await ibkrPersistence.PersistAsync(provider.Id, userId.Value, result, ct);

            provider.LastSyncAt = DateTime.UtcNow;
            provider.LastSyncStatus = "success";
            provider.LastSyncError = null;
            provider.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await historyService.SnapshotAllAccountsForDateAsync(today, ct);
            await narrativeService.RegenerateInvestmentNarrativeAsync(userId.Value, force: true, ct);

            return Ok(new { positions = result.Positions.Count, trades = result.Trades.Count, warning = result.Warning });
        }
        catch (Exception ex)
        {
            provider.LastSyncAt = DateTime.UtcNow;
            provider.LastSyncStatus = "failure";
            provider.LastSyncError = ex.Message;
            provider.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            logger.LogError(ex, "Manual IBKR sync failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("sync/status")]
    public async Task<IActionResult> GetSyncStatus(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var provider = await dbContext.InvestmentProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.ProviderType == InvestmentProviderType.Ibkr, ct);

        if (provider is null) return Ok(new { configured = false });

        return Ok(new
        {
            configured = true,
            enabled = provider.IsEnabled,
            lastSyncAt = provider.LastSyncAt,
            lastSyncStatus = provider.LastSyncStatus,
            lastSyncError = provider.LastSyncError
        });
    }

    [HttpGet("manual/accounts")]
    public async Task<ActionResult<List<ManualAccountDto>>> GetManualAccounts(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var manualProvider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.ProviderType == InvestmentProviderType.Manual, ct);
        if (manualProvider is null) return Ok(new List<ManualAccountDto>());

        var accounts = await dbContext.InvestmentAccounts
            .AsNoTracking()
            .Include(a => a.ManualBalance)
            .Where(a => a.ProviderId == manualProvider.Id && a.UserId == userId.Value)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.DisplayName)
            .ToListAsync(ct);

        return Ok(accounts.Select(a => new ManualAccountDto(
            Id: a.Id,
            DisplayName: a.DisplayName,
            AccountType: a.AccountType.ToString(),
            Currency: a.BaseCurrency,
            Balance: a.ManualBalance?.Balance,
            Icon: a.Icon,
            Color: a.Color,
            Notes: a.Notes,
            IsActive: a.IsActive,
            LastUpdated: a.ManualBalance?.UpdatedAt
        )).ToList());
    }

    [HttpPost("manual/accounts")]
    public async Task<IActionResult> CreateManualAccount([FromBody] CreateManualAccountRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var manualProvider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.ProviderType == InvestmentProviderType.Manual, ct);
        if (manualProvider is null) return BadRequest(new { error = "Manual provider not found" });

        if (!Enum.TryParse<AccountType>(request.AccountType, true, out var accountType))
            accountType = AccountType.Other;

        var account = new InvestmentAccount
        {
            ProviderId = manualProvider.Id,
            UserId = userId.Value,
            DisplayName = request.DisplayName,
            AccountType = accountType,
            BaseCurrency = request.Currency ?? "EUR",
            Icon = request.Icon ?? InvestmentAnalyticsService.DefaultIconForType(accountType),
            Color = request.Color ?? InvestmentAnalyticsService.DefaultColorForType(accountType),
            Notes = request.Notes,
            IsActive = true
        };
        dbContext.InvestmentAccounts.Add(account);

        if (request.InitialBalance.HasValue)
        {
            dbContext.ManualAccountBalances.Add(new ManualAccountBalance
            {
                AccountId = account.Id,
                Balance = request.InitialBalance.Value,
                Currency = request.Currency ?? "EUR",
                UpdatedAt = DateTime.UtcNow
            });

            dbContext.ManualBalanceHistories.Add(new ManualBalanceHistory
            {
                AccountId = account.Id,
                Balance = request.InitialBalance.Value,
                Currency = request.Currency ?? "EUR",
                RecordedAt = DateTime.UtcNow,
                Note = "Initial balance"
            });
        }

        await dbContext.SaveChangesAsync(ct);

        if (request.InitialBalance.HasValue)
            await historyService.SnapshotAccountAsync(account.Id, ct);

        return Ok(new { id = account.Id });
    }

    [HttpPatch("manual/accounts/{id:guid}")]
    public async Task<IActionResult> UpdateManualAccount(Guid id, [FromBody] UpdateManualAccountRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var account = await dbContext.InvestmentAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value, ct);
        if (account is null) return NotFound();

        if (request.DisplayName is not null) account.DisplayName = request.DisplayName;
        if (request.AccountType is not null && Enum.TryParse<AccountType>(request.AccountType, true, out var at))
            account.AccountType = at;
        if (request.Icon is not null) account.Icon = request.Icon;
        if (request.Color is not null) account.Color = request.Color;
        if (request.Notes is not null) account.Notes = request.Notes;
        if (request.IsActive.HasValue) account.IsActive = request.IsActive.Value;
        account.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpDelete("manual/accounts/{id:guid}")]
    public async Task<IActionResult> DeleteManualAccount(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var account = await dbContext.InvestmentAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value, ct);
        if (account is null) return NotFound();

        dbContext.InvestmentAccounts.Remove(account);
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("manual/accounts/{id:guid}/balance")]
    public async Task<IActionResult> UpdateBalance(Guid id, [FromBody] UpdateBalanceRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var account = await dbContext.InvestmentAccounts
            .Include(a => a.ManualBalance)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value, ct);
        if (account is null) return NotFound();

        if (account.ManualBalance is null)
        {
            account.ManualBalance = new ManualAccountBalance
            {
                AccountId = id,
                Balance = request.NewBalance,
                Currency = account.BaseCurrency,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.ManualAccountBalances.Add(account.ManualBalance);
        }
        else
        {
            account.ManualBalance.Balance = request.NewBalance;
            account.ManualBalance.UpdatedAt = DateTime.UtcNow;
        }

        dbContext.ManualBalanceHistories.Add(new ManualBalanceHistory
        {
            AccountId = id,
            Balance = request.NewBalance,
            Currency = account.BaseCurrency,
            RecordedAt = DateTime.UtcNow,
            Note = request.Note
        });

        await dbContext.SaveChangesAsync(ct);

        await historyService.SnapshotAccountAsync(id, ct);
        await narrativeService.RegenerateInvestmentNarrativeAsync(userId.Value, force: true, ct);

        return Ok(new { balance = request.NewBalance });
    }

    [HttpGet("manual/accounts/{id:guid}/history")]
    public async Task<IActionResult> GetBalanceHistory(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var account = await dbContext.InvestmentAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value, ct);
        if (account is null) return NotFound();

        var history = await dbContext.ManualBalanceHistories
            .AsNoTracking()
            .Where(h => h.AccountId == id)
            .OrderByDescending(h => h.RecordedAt)
            .Select(h => new { h.Balance, h.Currency, h.RecordedAt, h.Note })
            .ToListAsync(ct);

        return Ok(history);
    }
}

public record ManualAccountDto(
    Guid Id, string DisplayName, string AccountType, string Currency,
    decimal? Balance, string? Icon, string? Color, string? Notes,
    bool IsActive, DateTime? LastUpdated);

public record CreateManualAccountRequest(
    string DisplayName, string AccountType, string? Currency,
    decimal? InitialBalance, string? Icon, string? Color, string? Notes);

public record UpdateManualAccountRequest(
    string? DisplayName, string? AccountType, string? Icon,
    string? Color, string? Notes, bool? IsActive);

public record UpdateBalanceRequest(decimal NewBalance, string? Note);

}

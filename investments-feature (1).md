# Investments Feature — Spec

This is the third spec for the Expense Tracker, after `expense-tracker-improvements.md` and `dashboard-redesign-addendum.md`. It adds investment tracking alongside the existing expense tracking, sourced from two providers from day one: **Interactive Brokers** (via Flex Web Service) and **Manual entries** (for everything not in IBKR — Slovenian savings accounts, crypto, other brokers, etc.).

The goal: a unified personal finance view where spending, income, and investments live together, but each has its own appropriate level of detail. The expense dashboard remains the primary daily view; investments get a dedicated tab and a small at-a-glance strip on the main dashboard.

## Stack Reminder

- **Backend:** ASP.NET Core (.NET 10+), Clean Architecture (Api / Core / Infrastructure / Worker)
- **Database:** PostgreSQL with EF Core
- **Frontend:** Angular, Material/PrimeNG, ngx-charts
- **LLM:** Existing provider abstraction (`ILlmCategorizationProvider` + new `ILlmNarrativeProvider`)

This spec adds an analogous provider abstraction for investment data sources, with two implementations from day one: IBKR (API-driven) and Manual (user-entered).

## Guiding Principles

1. **Investments and expenses are different mental models.** Don't pollute the expense dashboard with detailed investment data — only a small at-a-glance strip. Detail lives on the dedicated `/investments` page.

2. **This is a "where am I, how am I doing" tool, not a trading platform.** IBKR's Client Portal and PortfolioAnalyst exist for trading and deep analysis. Don't replicate them.

3. **Daily sync is enough for IBKR. Manual entries are user-driven.** No real-time pricing, no sub-day updates, no automated price lookups for manual entries.

4. **Provider abstraction from day one.** IBKR and Manual are the first two providers. The architecture must allow adding others (Slovenian brokers, crypto exchanges, additional banks) without rewriting the dashboard.

5. **IBKR is source-of-truth for IBKR data.** Don't try to reconstruct positions from individual trades — fetch the canonical positions report from IBKR daily. Trades are stored for activity timeline, but holdings are pulled directly.

6. **Manual entries are cash-value only.** No quantity, no price lookup, no per-instrument breakdown. The user enters a single number per account ("my NLB savings: €4,200", "my crypto on Binance: €1,500"). Updates are user-driven, infrequent, and as detailed or as crude as the user wants.

7. **Total portfolio value is the sum of IBKR + Manual.** Allocation views and history charts include both sources unified. The user sees one portfolio, not two.

---

## Part 1: IBKR Flex Web Service Integration

### 1.1 Why Flex Web Service

IBKR offers three API access methods. For a daily portfolio tracker, **Flex Web Service** is the right choice:

- **TWS API / IB Gateway**: requires running TWS or IB Gateway as a desktop app continuously. Heavy infrastructure for a daily sync.
- **Client Portal Web API**: requires OAuth + active session, periodic re-auth. Designed for live trading, overkill here.
- **Flex Web Service**: simple HTTP token + query ID, perfect for server-side daily polling. Token lasts up to one year.

Flex Queries are pre-configured report templates created in IBKR Client Portal. The API generates a populated instance of the query and returns XML or CSV.

**Constraints:**
- Rate limit: ~1 request per second per token. We'll do 3-4 requests daily, no issue.
- Max date range per query: 365 days. For full history, paginate by year.
- Data is end-of-day, not real-time. Acceptable for this use case.
- Token expires up to one year. Need a UI to surface expiry and prompt renewal.

### 1.2 One-time IBKR setup (manual, documented for the user)

The user must perform these steps once in IBKR Client Portal before the integration can work:

1. Log into IBKR Client Portal
2. Settings → Account Settings → Reporting → Flex Web Service
3. Enable Flex Web Service, generate token, save token (shown only once)
4. Settings → Account Settings → Reporting → Flex Queries → Create three Activity Flex Queries:
   - **Positions**: includes Open Positions section, all available fields. Save query ID.
   - **Trades**: includes Trades section, all available fields. Save query ID.
   - **Cash Report**: includes Cash Report section. Save query ID.
   - **NAV (optional, for performance over time)**: includes Net Asset Value section. Save query ID.
5. Note: each query is "Activity" or "Trade Confirmation" — use Activity for these.
6. Date range setting: Last Business Day for daily snapshots; 365 days for backfill.

Document this in the Investments settings page in the app (see Part 8).

### 1.3 Flex Web Service flow

The API has a two-step flow per query:

**Step 1: Request report generation**
```
GET https://gdcdyn.interactivebrokers.com/Universal/servlet/FlexStatementService.SendRequest
    ?t={token}
    &q={queryId}
    &v=3
```
Returns XML containing a `ReferenceCode` (used to fetch the report) and a `Url` (where to fetch it).

**Step 2: Retrieve the generated report**
```
GET {Url}?q={referenceCode}&t={token}&v=3
```
Returns the XML report data, OR a status message indicating it's still generating (poll and retry).

If the report is still generating, IBKR returns a `<Status>Warn</Status>` with `<ErrorCode>1019</ErrorCode>`. Wait 1-2 seconds and retry. Reports typically generate in under 5 seconds.

### 1.4 .NET project structure

```
src/ExpenseTracker.Core/Investments/
  IInvestmentDataProvider.cs        (interface)
  InvestmentProviderType.cs         (enum: Ibkr, Manual)
  Models/
    Position.cs
    Trade.cs
    CashBalance.cs
    NavSnapshot.cs
    InvestmentSyncResult.cs

src/ExpenseTracker.Infrastructure/Investments/
  Ibkr/
    IbkrFlexProvider.cs             (implementation)
    IbkrFlexClient.cs               (HTTP client wrapper)
    IbkrFlexParser.cs               (XML parsing)
    IbkrFlexConfig.cs               (token, query IDs)
  Manual/
    ManualInvestmentProvider.cs     (implementation, no external API)

src/ExpenseTracker.Worker/Investments/
  InvestmentSyncWorker.cs           (BackgroundService)
```

### 1.5 Provider interface

```csharp
public interface IInvestmentDataProvider
{
    InvestmentProviderType ProviderType { get; }
    string DisplayName { get; }
    bool RequiresPeriodicSync { get; }   // true for IBKR, false for Manual
    
    Task<InvestmentSyncResult> SyncAsync(
        Guid providerId,
        CancellationToken cancellationToken);
    
    Task<ProviderTestResult> TestAsync(
        Guid providerId,
        CancellationToken cancellationToken);
}

public enum InvestmentProviderType
{
    Ibkr = 1,
    Manual = 2,
    // Future: Revolut = 3, Binance = 4, etc.
}

public record InvestmentSyncResult(
    IReadOnlyList<Position> Positions,
    IReadOnlyList<Trade> Trades,
    IReadOnlyList<CashBalance> CashBalances,
    NavSnapshot? NavSnapshot,
    DateTimeOffset SyncedAt,
    string? Warning);

public record Position(
    string Symbol,
    string Description,
    string AssetClass,        // "STK", "ETF", "BOND", "OPT", "FUT", "CASH", "CRYPTO"
    string Currency,
    decimal Quantity,
    decimal CostBasisPerShare,
    decimal MarkPrice,
    decimal MarketValue,
    decimal UnrealizedPnl,
    decimal UnrealizedPnlPercent,
    string? AccountId);

public record Trade(
    string TradeId,
    string Symbol,
    string AssetClass,
    string Currency,
    DateTime TradeDate,
    string BuySell,
    decimal Quantity,
    decimal Price,
    decimal Proceeds,
    decimal Commission,
    decimal NetCash,
    string? AccountId);

public record CashBalance(
    string Currency,
    decimal Amount,
    string? AccountId);

public record NavSnapshot(
    DateOnly Date,
    decimal TotalValue,
    string Currency,
    string? AccountId);
```

The `ManualInvestmentProvider` implementation never produces `Position` / `Trade` / `NavSnapshot` data via `SyncAsync` — it returns an empty result. Manual data is created via the API endpoints described in Part 4, not via sync. The interface is uniform so the dashboard can read from any provider, but the data flow is different.

### 1.6 IBKR Flex client implementation

```csharp
public class IbkrFlexClient
{
    private const string BaseUrl = "https://gdcdyn.interactivebrokers.com/Universal/servlet/FlexStatementService";
    private readonly HttpClient _http;
    
    public async Task<string> RequestAndFetchReportAsync(
        string token,
        string queryId,
        CancellationToken ct)
    {
        var requestUrl = $"{BaseUrl}.SendRequest?t={Uri.EscapeDataString(token)}&q={Uri.EscapeDataString(queryId)}&v=3";
        var requestResponse = await _http.GetStringAsync(requestUrl, ct);
        var (referenceCode, retrievalUrl) = ParseSendRequestResponse(requestResponse);
        
        var maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(2);
        
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var fetchUrl = $"{retrievalUrl}?q={Uri.EscapeDataString(referenceCode)}&t={Uri.EscapeDataString(token)}&v=3";
            var response = await _http.GetStringAsync(fetchUrl, ct);
            
            if (IsStillGenerating(response))
            {
                await Task.Delay(delay, ct);
                continue;
            }
            
            return response;
        }
        
        throw new TimeoutException("Flex Query did not complete within expected time");
    }
}
```

Use `IHttpClientFactory` with a named `IbkrFlex` client, polly retry policy: 3 attempts, exponential backoff with jitter, on transient HTTP errors.

### 1.7 XML parsing

IBKR Flex returns XML in a specific schema. Use `XDocument` for parsing.

Sample Position element:
```xml
<OpenPosition accountId="U1234567" symbol="VWCE" assetCategory="ETF" currency="EUR"
              position="50" markPrice="105.34" costBasisPrice="98.20"
              fifoPnlUnrealized="357.00" />
```

Sample Trade element:
```xml
<Trade transactionID="123456789" symbol="VWCE" assetCategory="ETF" currency="EUR"
       tradeDate="20260315" buySell="BUY" quantity="10" tradePrice="98.20"
       proceeds="-982.00" ibCommission="-1.50" netCash="-983.50" />
```

Parser must handle:
- Empty result sets
- Missing fields (null-safe attribute reads)
- Date format `yyyyMMdd` for `tradeDate`
- Decimal parsing with invariant culture
- Multiple accounts per response

### 1.8 IBKR Flex provider implementation

```csharp
public class IbkrFlexProvider : IInvestmentDataProvider
{
    public InvestmentProviderType ProviderType => InvestmentProviderType.Ibkr;
    public string DisplayName => "Interactive Brokers";
    public bool RequiresPeriodicSync => true;
    
    public async Task<InvestmentSyncResult> SyncAsync(Guid providerId, CancellationToken ct)
    {
        var config = await _configStore.GetAsync(providerId, ct);
        var token = await _configStore.DecryptApiKeyAsync(providerId, ct);
        
        var positionsXml = await _client.RequestAndFetchReportAsync(token, config.PositionsQueryId, ct);
        var tradesXml = await _client.RequestAndFetchReportAsync(token, config.TradesQueryId, ct);
        var cashXml = await _client.RequestAndFetchReportAsync(token, config.CashQueryId, ct);
        string? navXml = null;
        if (!string.IsNullOrEmpty(config.NavQueryId))
            navXml = await _client.RequestAndFetchReportAsync(token, config.NavQueryId, ct);
        
        return new InvestmentSyncResult(
            Positions: _parser.ParsePositions(positionsXml),
            Trades: _parser.ParseTrades(tradesXml),
            CashBalances: _parser.ParseCashBalances(cashXml),
            NavSnapshot: navXml != null ? _parser.ParseNav(navXml) : null,
            SyncedAt: DateTimeOffset.UtcNow,
            Warning: null);
    }
    
    public async Task<ProviderTestResult> TestAsync(Guid providerId, CancellationToken ct)
    {
        try
        {
            var config = await _configStore.GetAsync(providerId, ct);
            var token = await _configStore.DecryptApiKeyAsync(providerId, ct);
            var sw = Stopwatch.StartNew();
            var xml = await _client.RequestAndFetchReportAsync(token, config.PositionsQueryId, ct);
            sw.Stop();
            var positions = _parser.ParsePositions(xml);
            return new ProviderTestResult(true, $"Fetched {positions.Count} positions", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new ProviderTestResult(false, ex.Message, TimeSpan.Zero);
        }
    }
}
```

### 1.9 Manual provider implementation

```csharp
public class ManualInvestmentProvider : IInvestmentDataProvider
{
    public InvestmentProviderType ProviderType => InvestmentProviderType.Manual;
    public string DisplayName => "Manual entries";
    public bool RequiresPeriodicSync => false;
    
    public Task<InvestmentSyncResult> SyncAsync(Guid providerId, CancellationToken ct)
    {
        // Manual provider doesn't sync — data is created via API endpoints.
        return Task.FromResult(new InvestmentSyncResult(
            Positions: Array.Empty<Position>(),
            Trades: Array.Empty<Trade>(),
            CashBalances: Array.Empty<CashBalance>(),
            NavSnapshot: null,
            SyncedAt: DateTimeOffset.UtcNow,
            Warning: null));
    }
    
    public Task<ProviderTestResult> TestAsync(Guid providerId, CancellationToken ct)
    {
        return Task.FromResult(new ProviderTestResult(true, "Manual provider always available", TimeSpan.Zero));
    }
}
```

The Manual provider is essentially a placeholder for the interface contract. Real data lives in `manual_account_balances` (Part 2.4) and is written via API endpoints, not via `SyncAsync`.

### 1.10 Background worker

```csharp
public class InvestmentSyncWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllProvidersAsync(stoppingToken);
                await SnapshotPortfolioHistoryAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Investment sync failed");
            }
            
            var nextRun = ComputeNextRunTime();
            var delay = nextRun - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);
        }
    }
    
    private async Task SyncAllProvidersAsync(CancellationToken ct)
    {
        var enabledProviders = await _configStore.GetEnabledProvidersAsync(ct);
        foreach (var providerConfig in enabledProviders)
        {
            var provider = _providerResolver.Resolve(providerConfig.ProviderType);
            if (!provider.RequiresPeriodicSync)
                continue;  // Skip Manual
            
            var result = await provider.SyncAsync(providerConfig.Id, ct);
            await _persistenceService.PersistAsync(providerConfig.Id, result, ct);
            await _configStore.UpdateLastSyncAsync(providerConfig.Id, result.SyncedAt, ct);
        }
    }
    
    private async Task SnapshotPortfolioHistoryAsync(CancellationToken ct)
    {
        // Compute today's value per account (IBKR holdings + manual balances) and write snapshots.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await _historyService.SnapshotAllAccountsForDateAsync(today, ct);
    }
}
```

Also expose a manual trigger endpoint (`POST /api/investments/sync`) for ad-hoc IBKR syncs.

---

## Part 2: Database Schema

All money fields: `decimal(18,4)`. Timestamps: `timestamptz` UTC. Currency: ISO 4217.

### 2.1 Configuration tables

**investment_providers**

Mirrors the structure of `llm_providers`.

```sql
CREATE TABLE investment_providers (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    provider_type text NOT NULL,            -- 'ibkr' | 'manual'
    display_name text NOT NULL,
    api_token_encrypted text,               -- IBKR Flex token; null for manual
    extra_config jsonb,                     -- query IDs, etc.
    is_enabled boolean NOT NULL DEFAULT false,
    last_sync_at timestamptz,
    last_sync_status text,                  -- 'success' | 'failure' | 'never' | 'n/a'
    last_sync_error text,
    last_test_at timestamptz,
    last_test_status text,                  -- 'success' | 'failure' | 'untested'
    last_test_error text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (provider_type)
);
```

For IBKR, `extra_config` JSON shape:
```json
{
    "positionsQueryId": "1234567",
    "tradesQueryId": "1234568",
    "cashQueryId": "1234569",
    "navQueryId": "1234570",
    "tokenExpiresAt": "2027-05-09T00:00:00Z",
    "accountIds": ["U1234567"]
}
```

For Manual, `extra_config` is `null` or `{}`.

Both providers can be enabled simultaneously. Seed both rows on first startup with `is_enabled = false`.

### 2.2 Shared investment account table

**investment_accounts**

Used by both IBKR and Manual. Each row represents one account whose value contributes to the total portfolio.

```sql
CREATE TABLE investment_accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    provider_id uuid NOT NULL REFERENCES investment_providers(id) ON DELETE CASCADE,
    external_account_id text,             -- IBKR account ID; null for manual
    display_name text NOT NULL,           -- "IBKR Main", "NLB Savings", "Binance Crypto"
    account_type text NOT NULL,           -- 'broker' | 'savings' | 'crypto' | 'cash' | 'pension' | 'real_estate' | 'other'
    base_currency text NOT NULL DEFAULT 'EUR',
    icon text,                            -- Material icon name
    color text,                           -- hex
    is_active boolean NOT NULL DEFAULT true,
    sort_order int NOT NULL DEFAULT 0,
    notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);
```

For IBKR: one row per linked IBKR sub-account, populated automatically during sync, `display_name` defaults to `external_account_id` and is editable.

For Manual: rows are created by the user via the UI. `external_account_id` is null. `display_name` is whatever the user types.

### 2.3 IBKR-specific tables

**instruments**

Reusable metadata about a security.

```sql
CREATE TABLE instruments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    symbol text NOT NULL,
    name text,
    asset_class text NOT NULL,            -- 'STK' | 'ETF' | 'BOND' | 'OPT' | 'FUT' | 'CASH' | 'CRYPTO' | 'OTHER'
    currency text NOT NULL,
    sector text,
    region text,
    isin text,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (symbol, asset_class, currency)
);
```

**holdings**

Current snapshot of IBKR positions, replaced on each sync.

```sql
CREATE TABLE holdings (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES investment_accounts(id) ON DELETE CASCADE,
    instrument_id uuid NOT NULL REFERENCES instruments(id),
    quantity decimal(18,4) NOT NULL,
    cost_basis_per_share decimal(18,4),
    mark_price decimal(18,4),
    market_value decimal(18,4) NOT NULL,
    unrealized_pnl decimal(18,4),
    unrealized_pnl_percent decimal(10,4),
    currency text NOT NULL,
    as_of timestamptz NOT NULL,
    UNIQUE (account_id, instrument_id)
);
```

**investment_transactions**

IBKR trades, dividends, fees, etc. Idempotent.

```sql
CREATE TABLE investment_transactions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES investment_accounts(id) ON DELETE CASCADE,
    instrument_id uuid REFERENCES instruments(id),
    external_transaction_id text NOT NULL,
    transaction_type text NOT NULL,        -- 'BUY' | 'SELL' | 'DIVIDEND' | 'DEPOSIT' | 'WITHDRAWAL' | 'FEE' | 'INTEREST' | 'FX' | 'OTHER'
    transaction_date timestamptz NOT NULL,
    quantity decimal(18,4),
    price decimal(18,4),
    gross_amount decimal(18,4),
    commission decimal(18,4) DEFAULT 0,
    net_amount decimal(18,4) NOT NULL,
    currency text NOT NULL,
    description text,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (account_id, external_transaction_id)
);
```

This table only holds IBKR transactions. Manual accounts don't have a transaction concept in this MVP — only a current balance.

### 2.4 Manual-specific tables

**manual_account_balances**

The current cash value of each manual account.

```sql
CREATE TABLE manual_account_balances (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL UNIQUE REFERENCES investment_accounts(id) ON DELETE CASCADE,
    balance decimal(18,4) NOT NULL,
    currency text NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);
```

Just a number per account. No quantity, no prices, no holdings breakdown.

**manual_balance_history**

Append-only log of every balance update.

```sql
CREATE TABLE manual_balance_history (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES investment_accounts(id) ON DELETE CASCADE,
    balance decimal(18,4) NOT NULL,
    currency text NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT now(),
    note text
);

CREATE INDEX ix_manual_balance_history_account_date 
    ON manual_balance_history (account_id, recorded_at DESC);
```

When the user updates a manual balance:
1. Update `manual_account_balances` row (or insert if first time)
2. Insert a new `manual_balance_history` row

This gives a discrete history of balance changes for the chart and activity timeline.

### 2.5 Unified portfolio history

**portfolio_history**

One row per day per account, capturing the value of each account at end-of-day.

```sql
CREATE TABLE portfolio_history (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES investment_accounts(id) ON DELETE CASCADE,
    snapshot_date date NOT NULL,
    market_value decimal(18,4) NOT NULL,
    currency text NOT NULL,
    source text NOT NULL,                  -- 'sync' (IBKR) | 'manual'
    UNIQUE (account_id, snapshot_date)
);
```

Population logic during the daily worker run:

- **For IBKR accounts**: after sync completes, sum the `market_value` of all holdings + cash balances per account, write one row.
- **For Manual accounts**: take the current `manual_account_balances.balance` and write today's snapshot. If the user hasn't updated the balance in N days, the snapshot just carries forward the last-known value.

This means manual accounts will have flat lines on the history chart between balance updates, which is correct — the system doesn't know what happened in between.

The unique constraint on `(account_id, snapshot_date)` ensures one snapshot per day with last-write-wins semantics.

### 2.6 No archival in MVP

Don't add pruning logic. Daily snapshots over 5 years = ~1800 rows per account, trivial in Postgres.

---

## Part 3: Backend Services

### 3.1 IBKR persistence service

```csharp
public class IbkrPersistenceService
{
    public async Task PersistAsync(Guid providerId, InvestmentSyncResult result, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        
        try
        {
            var externalAccountIds = result.Positions
                .Select(p => p.AccountId)
                .Concat(result.Trades.Select(t => t.AccountId))
                .Concat(result.CashBalances.Select(c => c.AccountId))
                .Where(id => id != null)
                .Distinct()
                .ToList();
            
            foreach (var externalId in externalAccountIds)
                await EnsureIbkrAccountExistsAsync(providerId, externalId, ct);
            
            var instrumentKeys = result.Positions.Select(p => (p.Symbol, p.AssetClass, p.Currency))
                .Concat(result.Trades.Select(t => (t.Symbol, t.AssetClass, t.Currency)))
                .Distinct();
            
            foreach (var (symbol, assetClass, currency) in instrumentKeys)
                await EnsureInstrumentExistsAsync(symbol, assetClass, currency, ct);
            
            await ReplaceHoldingsAsync(externalAccountIds, result.Positions, result.CashBalances, ct);
            await InsertTransactionsIdempotentAsync(result.Trades, ct);
            
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

Holdings replace wholesale. Transactions upsert via unique constraint. Whole sync is one DB transaction.

### 3.2 Manual account service

```csharp
public class ManualAccountService
{
    public async Task<Guid> CreateAccountAsync(CreateManualAccountDto dto, CancellationToken ct)
    {
        var providerId = await _configStore.GetIdAsync(InvestmentProviderType.Manual, ct);
        
        var account = new InvestmentAccount
        {
            ProviderId = providerId,
            ExternalAccountId = null,
            DisplayName = dto.DisplayName,
            AccountType = dto.AccountType,
            BaseCurrency = dto.Currency,
            Icon = dto.Icon ?? DefaultIconForType(dto.AccountType),
            Color = dto.Color ?? DefaultColorForType(dto.AccountType),
            Notes = dto.Notes,
            IsActive = true
        };
        
        _db.InvestmentAccounts.Add(account);
        
        if (dto.InitialBalance.HasValue)
        {
            _db.ManualAccountBalances.Add(new ManualAccountBalance
            {
                AccountId = account.Id,
                Balance = dto.InitialBalance.Value,
                Currency = dto.Currency,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            
            _db.ManualBalanceHistory.Add(new ManualBalanceHistoryEntry
            {
                AccountId = account.Id,
                Balance = dto.InitialBalance.Value,
                Currency = dto.Currency,
                RecordedAt = DateTimeOffset.UtcNow,
                Note = "Initial balance"
            });
        }
        
        await _db.SaveChangesAsync(ct);
        return account.Id;
    }
    
    public async Task UpdateBalanceAsync(Guid accountId, decimal newBalance, string? note, CancellationToken ct)
    {
        var current = await _db.ManualAccountBalances
            .FirstOrDefaultAsync(b => b.AccountId == accountId, ct);
        
        if (current == null)
        {
            var account = await _db.InvestmentAccounts.FindAsync(accountId);
            current = new ManualAccountBalance
            {
                AccountId = accountId,
                Balance = newBalance,
                Currency = account.BaseCurrency,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.ManualAccountBalances.Add(current);
        }
        else
        {
            current.Balance = newBalance;
            current.UpdatedAt = DateTimeOffset.UtcNow;
        }
        
        _db.ManualBalanceHistory.Add(new ManualBalanceHistoryEntry
        {
            AccountId = accountId,
            Balance = newBalance,
            Currency = current.Currency,
            RecordedAt = DateTimeOffset.UtcNow,
            Note = note
        });
        
        await _db.SaveChangesAsync(ct);
    }
}
```

Default icons/colors per account type:

| account_type | icon | color |
|---|---|---|
| broker | trending_up | #2196F3 |
| savings | savings | #4CAF50 |
| crypto | currency_bitcoin | #FF9800 |
| cash | payments | #9E9E9E |
| pension | account_balance | #9C27B0 |
| real_estate | home | #795548 |
| other | category | #607D8B |

### 3.3 Currency conversion

Holdings can be in multiple currencies. Manual balances are entered in the account's `base_currency`.

```csharp
public interface ICurrencyConverter
{
    Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateOnly asOf, CancellationToken ct);
}
```

Implementation: ECB daily reference rates, cached in `currency_rates` table refreshed daily. Support EUR, USD, GBP, CHF.

### 3.4 Analytics service

```csharp
public interface IInvestmentAnalyticsService
{
    Task<PortfolioSummary> GetSummaryAsync(string baseCurrency, CancellationToken ct);
    Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(string baseCurrency, CancellationToken ct);
    Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(string baseCurrency, CancellationToken ct);
    Task<AllocationBreakdown> GetAllocationAsync(AllocationType type, string baseCurrency, CancellationToken ct);
    Task<IReadOnlyList<HistoryPoint>> GetHistoryAsync(DateOnly from, DateOnly to, string baseCurrency, CancellationToken ct);
    Task<IReadOnlyList<RecentActivityItem>> GetRecentActivityAsync(int limit, CancellationToken ct);
    Task<decimal> ComputeTotalPortfolioValueAsync(string baseCurrency, CancellationToken ct);
}

public enum AllocationType { AssetClass, AccountType, Account, Currency }

public record PortfolioSummary(
    decimal TotalValue,
    decimal IbkrValue,
    decimal ManualValue,
    decimal? DayChange,
    decimal? DayChangePercent,
    decimal? YtdChange,
    decimal? YtdChangePercent,
    string BaseCurrency,
    DateTimeOffset AsOf,
    int? OldestManualUpdateDays);   // for "stale balance" warning

public record AccountSummary(
    Guid AccountId,
    string DisplayName,
    string AccountType,
    string ProviderType,           // "ibkr" | "manual"
    string Icon,
    string Color,
    decimal Value,
    string Currency,
    decimal ValueInBaseCurrency,
    DateTimeOffset? LastUpdated,
    int? DaysSinceUpdate);
```

Key implementation notes:

- `TotalValue` = sum of IBKR holdings + IBKR cash + all manual balances, all converted to `baseCurrency`
- `DayChange` = today's `portfolio_history` total minus yesterday's. Returns null if either snapshot doesn't exist.
- Allocation views distinguish manual accounts as their own asset class
- `OldestManualUpdateDays` lets the UI flag stale balances (>30 days)

### 3.5 Recent activity

Combine IBKR transactions and manual balance updates:

```csharp
public record RecentActivityItem(
    DateTimeOffset Date,
    Guid AccountId,
    string AccountDisplayName,
    string ProviderType,           // "ibkr" | "manual"
    string ActivityType,           // "BUY" | "SELL" | "DIVIDEND" | ... | "BALANCE_UPDATE"
    string Description,
    decimal? Amount,               // signed
    string Currency,
    decimal? Quantity,
    string? InstrumentSymbol);
```

Manual balance updates appear as `BALANCE_UPDATE` entries with description like "Updated NLB Savings: €4,200 (was €4,150, +€50)". Pulled from `manual_balance_history`.

---

## Part 4: API Endpoints

All under `/api/investments`, all require auth.

### 4.1 Read endpoints (work for both providers)

```
GET  /api/investments/summary
GET  /api/investments/accounts
GET  /api/investments/holdings              (IBKR only — empty for manual-only setups)
GET  /api/investments/allocation?type=assetClass|accountType|account|currency
GET  /api/investments/history?from=&to=&granularity=day|week|month
GET  /api/investments/activity?limit=50
GET  /api/investments/dashboard-strip
GET  /api/investments/narrative
```

### 4.2 IBKR-specific endpoints

```
POST /api/investments/sync
GET  /api/investments/sync/status
```

### 4.3 Manual account endpoints

```
GET    /api/investments/manual/accounts
       → list manual accounts with current balance and last update

POST   /api/investments/manual/accounts
       → body: { displayName, accountType, currency, initialBalance?, icon?, color?, notes? }

PATCH  /api/investments/manual/accounts/{id}
       → body: { displayName?, accountType?, icon?, color?, notes?, isActive? }
       → update metadata (not balance)

DELETE /api/investments/manual/accounts/{id}

POST   /api/investments/manual/accounts/{id}/balance
       → body: { newBalance, note? }
       → update current balance, append history entry

GET    /api/investments/manual/accounts/{id}/history
       → balance history for one account
```

### 4.4 Provider configuration endpoints

```
GET   /api/investment-providers
PATCH /api/investment-providers/{id}            (IBKR token/config)
POST  /api/investment-providers/{id}/test
POST  /api/investment-providers/{id}/enable
POST  /api/investment-providers/{id}/disable
```

---

## Part 5: Frontend — Main Dashboard Integration

### 5.1 Investments strip on main dashboard

Below the spending strip and narrative:

```
SPENDING STRIP:    This month €566 · On pace €1,750 · Net 30d +€350
                   [LLM narrative for spending, italic]

INVESTMENTS STRIP: Portfolio €X,XXX · Today +€XX (+X.X%) · YTD +€X,XXX (+XX%)
                   [LLM narrative for investments, italic, optional]
```

**Implementation:**
- New `InvestmentsStripComponent` calls `GET /api/investments/dashboard-strip`
- Render below spending strip
- If neither IBKR nor Manual provider has any data, render nothing
- If at least one Manual account exists but no IBKR sync yet, the strip works fine (sums manual balances)
- Click → navigate to `/investments`
- Day/YTD changes colored green/red
- If today's history snapshot doesn't exist yet, show `Portfolio €X,XXX · — · —`

The investments strip is **subordinate** to the spending strip — slightly smaller text, slightly muted.

### 5.2 Sidebar navigation

```
PRIMARY
  📊 Dashboard
  🧾 Transactions
  📈 Investments

REPORTS
  ...

CONFIGURATION
  ...

SETTINGS
  ⚡ LLM
  💼 Investment providers   ← new
  ...
```

---

## Part 6: Frontend — Investments Page

Path: `/investments`. Layout mirrors the redesigned expense dashboard.

### 6.1 Top strip

```
Total value €X,XXX  ·  YTD +€X,XXX (+X.X%)  ·  IBKR €X,XXX  ·  Manual €X,XXX
```

Below: LLM narrative, italic, one line max.

If any manual balance is >30 days old:

```
⚠ Some manual balances haven't been updated in over a month. Update them →
```

Plain inline warning, links to the manual accounts section.

### 6.2 Primary widgets

**Top row (two columns):**

**Left widget: Accounts overview**

Lists every account (IBKR sub-accounts + manual accounts) sorted by value descending.

```
● 📈 IBKR Main           Broker · IBKR              €8,409
● 💰 NLB Savings         Savings · Manual           €4,200    Updated 3 days ago
● ₿  Binance Crypto      Crypto · Manual            €1,500    Updated 12 days ago
● 🏦 NKBM Cash           Cash · Manual                €620    Updated 28 days ago  ⚠
```

Each row:
- Color dot from account
- Icon from account
- Display name (bold)
- Account type subtitle ("Broker · IBKR" or "Savings · Manual")
- Value (right-aligned, primary)
- For Manual accounts: "Updated X days ago" (small, secondary)
- ⚠ icon next to manual accounts updated >30 days ago

For Manual rows, click → opens balance edit dialog.
For IBKR rows, click → drills into holdings list filtered to that account.

Header: "Accounts" + count + total value.

**Right widget: Allocation**

Same leaderboard format as expense category leaderboard. Toggle: `[ Asset class ]  [ Account type ]  [ Account ]  [ Currency ]`.

Default view: Account type.

```
● Brokerage     €8,409   58%   ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
● Savings       €4,200   29%   ▓▓▓▓▓▓▓▓
● Crypto        €1,500   10%   ▓▓▓
● Cash            €620    4%   ▓
```

For "Asset class" view, IBKR holdings are categorized by asset class, manual accounts mapped:

| account_type | mapped asset class |
|---|---|
| broker (manual) | Stocks |
| savings | Cash |
| crypto | Crypto |
| cash | Cash |
| pension | Bonds |
| real_estate | Real Estate |
| other | Other |

### 6.3 Portfolio value over time (full width)

Line chart, default last 12 months, options for 1mo / 3mo / YTD / 1y / all.

Lines:
- **Total portfolio value** (primary)
- **Toggle**: "Stack by account" — switches to stacked area chart, each layer is one account. Off by default.

Manual accounts have flat lines between balance updates (correct — system doesn't fabricate intermediate values).

Tooltip: date, total, breakdown by account.

### 6.4 Recent activity

Same row format as expense Recent transactions widget. Last 15 items combining IBKR transactions and manual balance updates:

```
● 09 May  BUY     VWCE          10 @ €105.34   −€1,053.40
● 08 May  DIV     VOO                    —     +€18.75
● 07 May  UPDATE  NLB Savings           —     €4,200 (was €4,150)
● 05 May  DEPOSIT IBKR                  —     +€2,000.00
● 03 May  UPDATE  Binance Crypto        —     €1,500 (was €1,650)
```

UPDATE entries from `manual_balance_history`.

### 6.5 Manual accounts section (below activity)

Critical section for manual-entry UX.

```
MANUAL ACCOUNTS                                        [+ Add account]

● 💰 NLB Savings              €4,200      Updated 3 days ago    [Update]
● ₿  Binance Crypto           €1,500      Updated 12 days ago   [Update]
● 🏦 NKBM Cash                  €620      Updated 28 days ago   [Update]
● 🏠 Apartment value         €185,000     Updated 95 days ago ⚠ [Update]
```

Each row:
- Color dot + icon + name
- Current balance
- Last updated indicator (with ⚠ if >30 days)
- "Update" button → opens dialog:

```
┌──────────────────────────────────────────┐
│  Update NLB Savings                       │
│                                           │
│  Current balance: €4,200                  │
│  New balance:    [_______________]        │
│  Note (optional): [_______________]       │
│                                           │
│           [Cancel]   [Save]               │
└──────────────────────────────────────────┘
```

After save: balance updated, history entry written, UI refreshes to "Updated just now".

The "+ Add account" button opens a similar form: display name, account type dropdown, currency, optional initial balance, optional notes.

This section is critical for the manual-entry UX. Updating a balance has to be frictionless — one button click from the Investments page, one form, two fields, save.

### 6.6 No performance-vs-benchmark in MVP

Skip this for now. Adds complexity (benchmark price feeds, currency-aware returns) without much daily value. Add later if desired.

---

## Part 7: LLM Narrative

Add a new narrative type to the `summaries` table: `summary_type = 'investments'`, `scope = 'current'`.

### 7.1 Investments narrative prompt

User prompt template:

```
Write ONE sentence (max 15 words) describing the investment portfolio status.

Style requirements:
- Lead with the takeaway, not the math
- Don't start with "Your portfolio is" — start with the observation
- Reference one specific account or trend if it carries the meaning
- Match the tone of these examples:
  - "Up 8% YTD, mostly driven by IBKR holdings; manual accounts steady."
  - "Mixed picture — brokerage gains offset by stale crypto balance."
  - "Down 3% this month following the broader market correction."
  - "Allocation tilting toward cash; manual savings growing while IBKR is flat."

Input data:
- Total value: €{totalValue}
- Day change: {daySign}€{dayChange} ({dayPercent}%)
- YTD change: {ytdSign}€{ytdChange} ({ytdPercent}%)
- IBKR value: €{ibkrValue} ({ibkrPercent}%)
- Manual value: €{manualValue} ({manualPercent}%)
- Account type breakdown: {accountTypeSummary}
- Largest single account: {largestAccountName} (€{largestAccountValue})
- Days since most recent manual balance update: {daysSinceManualUpdate}

Output: ONE sentence. No greeting, no padding.
```

### 7.2 Cache and regeneration

Same as expense narratives:
- Hash inputs into `cache_key`
- Worker regenerates after IBKR sync completes
- Worker also regenerates when a manual balance is updated
- API reads from cache, returns null if no entry exists

---

## Part 8: Settings — Investment Providers Page

Path: `/settings/investments`. Two cards.

### 8.1 IBKR card

- Title: "Interactive Brokers"
- Status indicator: enabled/disabled, last sync timestamp, last sync status
- Token field: masked, with "Update" button. Shows "Token expires: YYYY-MM-DD". Red highlight if expires within 30 days.
- Query IDs: four input fields (Positions, Trades, Cash Report, NAV optional)
- Account ID filter (optional): comma-separated list
- "Test connection" button
- "Enabled" toggle
- "Sync now" button
- Documentation accordion: "How to set up IBKR Flex Web Service →"

### 8.2 Manual card

- Title: "Manual entries"
- Status indicator: always enabled, no token needed
- Description: "Track accounts that aren't connected via API — savings accounts, crypto, cash, etc. You enter the balance manually and update it whenever you like."
- Stat line: "X manual accounts · Total value €X,XXX · Oldest update: N days ago"
- Button: "Manage manual accounts →" links to manual accounts section on `/investments`
- No configuration needed; toggle stays enabled

### 8.3 Token expiry handling for IBKR

- Store `tokenExpiresAt` in `extra_config` JSON (user input)
- Show countdown: "Expires in 47 days" / "Expires in 3 days" (red)
- If expired or expiring within 7 days, banner on dashboard and `/investments`
- When user updates the token, prompt for new expiry date

### 8.4 IBKR setup documentation (accordion)

```
How to set up IBKR Flex Web Service:

1. Log into IBKR Client Portal
2. Settings → Account Settings → Reporting → Flex Web Service
3. Enable Flex Web Service, generate token (shown once — save it)
4. Settings → Account Settings → Reporting → Flex Queries
5. Create three Activity Flex Queries:
   - "Positions": Sections = Open Positions, all fields
   - "Trades": Sections = Trades, all fields
   - "Cash Report": Sections = Cash Report, all fields
   - (Optional) "NAV": Sections = Net Asset Value, all fields
6. For each, set Date Range = "Last Business Day"
7. Save each query, copy the Query ID from the URL
8. Enter token + Query IDs in fields above
9. "Test connection", then enable

Sync runs daily at 23:00 UTC. Trigger manually with "Sync now".
```

---

## Part 9: Build Order

**Phase 1 — Schema and provider scaffolding**
1. Add all tables in single EF migration: `investment_providers`, `investment_accounts`, `instruments`, `holdings`, `investment_transactions`, `manual_account_balances`, `manual_balance_history`, `portfolio_history`, `currency_rates`
2. Seed `investment_providers` with IBKR and Manual rows (`is_enabled = false`)
3. Define `IInvestmentDataProvider` interface
4. Implement `ManualInvestmentProvider` (trivial)
5. Implement `ManualAccountService`

**Phase 2 — Manual entries UI (build first, before IBKR)**
6. Manual account API endpoints (CRUD + balance update + history)
7. Manual accounts section on `/investments` page (list, add, update modal)
8. Investments page top strip (works with just manual data — sums manual balances)
9. Accounts overview widget (shows manual accounts at this point)
10. Allocation widget (account type breakdown using only manual data)
11. Wire portfolio history snapshot for manual accounts (on every balance update + daily by worker)
12. Portfolio value chart (works with manual-only data)
13. Recent activity (shows manual balance updates at this point)
14. Manual card on `/settings/investments`

After Phase 2, the user can already track and visualize manual accounts.

**Phase 3 — IBKR integration**
15. Implement `IbkrFlexClient`, `IbkrFlexParser`, `IbkrFlexProvider`
16. Implement `IbkrPersistenceService`
17. Implement `InvestmentSyncWorker` background service
18. IBKR sync trigger endpoint
19. IBKR card on `/settings/investments`
20. Holdings widget on `/investments` (IBKR positions)
21. Update accounts overview to include IBKR sub-accounts
22. Update allocation views to include IBKR (asset class breakdown becomes meaningful)
23. Update activity to include IBKR trades

**Phase 4 — Dashboard integration**
24. `dashboard-strip` endpoint
25. `InvestmentsStripComponent` on main dashboard
26. Render only if at least one provider has data

**Phase 5 — Polish**
27. Token expiry banner for IBKR
28. Stale balance warning for manual accounts (>30 days)
29. Currency conversion if multi-currency holdings appear
30. Account icon/color customization UI

**Phase 6 — LLM narrative**
31. Investments narrative type in `summaries` table
32. Implement narrative prompt
33. Wire regeneration triggers (post-sync, post-manual-update)
34. Render narrative on investments page
35. (Optional) Render on main dashboard investments strip

The system is genuinely useful after Phase 2 (manual-only tracking works). After Phase 4 it's the unified dashboard. Phases 5-6 are polish.

**Why Manual first:**

Building Manual first means a working investments page with real data within a day or two, before tackling IBKR's API complexity. It also forces the schema and UI patterns to be source-agnostic from the start, preventing IBKR from leaking assumptions into the architecture.

---

## Part 10: Cross-Cutting Concerns

### 10.1 Currency

- IBKR holdings can be in multiple currencies
- Manual balances entered in account's `base_currency`
- Default base currency: EUR
- Summary endpoints accept `baseCurrency` query param
- Conversion uses ECB daily rates, cached in `currency_rates`

For MVP, support EUR and USD only. Add others as needed.

### 10.2 Time zones

- IBKR sync schedule: UTC
- IBKR `tradeDate` parsed as UTC
- Display in Europe/Ljubljana
- All timestamps stored in UTC

### 10.3 Failure handling

- IBKR API down → log error, leave existing data, dashboard shows stale data with warning
- IBKR token expired → sync fails, dashboard banner appears
- Single position parse error → log warning, skip that position, continue
- Manual balance update conflict → not possible (no concurrency on personal use)

### 10.4 Privacy / security

- IBKR token encrypted at rest using existing Data Protection / Key Vault setup
- No encryption needed for manual balances (just numbers in your own DB)
- Audit log entries when IBKR token is rotated or providers enabled/disabled
- Don't log full token contents

### 10.5 Cost monitoring

- IBKR Flex Web Service: free
- LLM narrative should run once daily, same as everything else.
- Currency rate fetches: 1 per day from ECB, free
- Total operational cost increase: ~0

---

## Notes for Claude Code

- **Don't break existing expense functionality.** Investments is purely additive.
- **Build Manual provider first, then IBKR.** Manual is simpler, gives you a working investments page faster, and forces the architecture to be source-agnostic.
- **Reuse the LLM provider abstraction.** Investment narrative uses the same `ILlmNarrativeProvider` interface — just a new prompt template and cache scope.
- **Reuse the encryption pattern.** IBKR token uses the same Data Protection / Key Vault approach as LLM API keys.
- **Manual balances have no calculations.** No price lookup, no quantity tracking, no cost basis. Single number per account in the account's currency.
- **Idempotency matters for IBKR.** Holdings replace, transactions upsert, history snapshots upsert on date.
- **Empty states matter.** A user might enable Manual without IBKR, or IBKR without Manual, or both, or neither. Every page must render cleanly in all four states.
- **The accounts overview is the centerpiece.** When the user clicks "Investments", the most useful thing they see is the list of accounts with current values. Make it scannable, glanceable, clickable.
- **Test IBKR with a paper trading account first.** Same Flex Query API, no risk.

The end state: open the dashboard, see your spending and your portfolio together, both interpreted in plain English. Click into Investments, see all your accounts (whether broker-connected or manually-tracked) in one view, update balances with one click, see the unified history of your money over time.

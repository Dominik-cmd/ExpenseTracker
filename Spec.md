# Plan: ExpenseTracker — Full Local Implementation

**TL;DR:** Complete spec implementation (all analytics, seed data, all UI pages) as a Docker Compose stack — .NET 10 API + Angular/PrimeNG/ECharts + Postgres. Excludes only Azure, CI/CD, backups, and timezone conversion. Three SMS formats parsed, 2-level category hierarchy, LLM suggests both category and subcategory.

---

## SMS Formats (3 patterns)

| Pattern | Direction | Type |
|---|---|---|
| `Odliv DD.MM.YYYY; Iz racuna: ...; Prejemnik: NAME; Namen: PURPOSE; Znesek: X EUR OTP banka d.d.` | debit | transfer_out |
| `Priliv DD.MM.YYYY; Racun: ...; Placnik: NAME; Namen: PURPOSE; Znesek: X EUR OTP banka d.d.` | credit | transfer_in |
| `POS NAKUP DD.MM.YYYY HH:MM, kartica ***XXXX, znesek X EUR, MERCHANT, CITY CC. Info: PHONE. OTP banka` | debit | purchase |

Parsing notes: comma→dot for amount, city (`MARIBOR SI`) stripped from merchant during normalization, `Namen` stored in `notes` and passed to LLM, POS ends with `OTP banka` (no `d.d.`).

---

## Schema Changes vs. Spec (Subcategories)

`categories` gets `parent_category_id uuid nullable fk→categories (self-ref)`. Top-level categories have `parent_category_id = null`. Max 2 levels enforced in application layer.

`transactions.category_id` stays a single field — points to the leaf node (subcategory if assigned, top-level otherwise). Parent resolved via JOIN on `categories.parent_category_id` in analytics.

`CategorizationResult` gets nullable `SubcategoryName`. LLM prompt lists categories hierarchically and returns `{"category": "Groceries", "subcategory": "Mercator", "confidence": 0.95, "reasoning": "..."}`. If subcategory doesn't exist it is auto-created under the resolved parent.

---

## Phase 1 — Backend Foundation

1. Create solution `ExpenseTracker.sln` with projects: `Api`, `Core`, `Infrastructure`, `UnitTests`, `IntegrationTests` (Worker hosted inside Api as `BackgroundService`)
2. **Core project**: domain entities (`User`, `RawMessage`, `Transaction`, `Category`, `MerchantRule`, `LlmProvider`, `AuditLog`, `Setting`), all enums (`ParseStatus`, `Direction`, `TransactionType`, `CategorySource`, `TransactionSource`, `LlmProviderType`, `LlmTestStatus`), interfaces (`ILlmCategorizationProvider`, `ILlmProviderResolver`), record types (`CategorizationRequest` — includes `Purpose` from Namen, `CategorizationResult` — includes nullable `SubcategoryName`, `ProviderTestResult`)
3. **Infrastructure project**: `AppDbContext` with all 8 tables, fluent config including partial unique index on `llm_providers WHERE is_enabled = true`, self-referential FK on `categories.parent_category_id`; Data Protection API configured with keys persisted to filesystem path from env var `DATA_PROTECTION_KEY_PATH`
4. Initial EF Core migration, auto-applied on startup in dev environment
5. Idempotent seed on startup:
   - User `dominik` (BCrypt factor 12, password from `INITIAL_PASSWORD` env var, loud warning if default used)
   - 20 top-level categories as per spec table (sort_order 1–20, 99 for Uncategorized)
   - Subcategories: Mercator/Hofer/Lidl/Spar/Tus → Groceries; Petrol/OMV → Fuel; Netflix/Spotify → Subscriptions
   - 3 LLM provider rows (all `is_enabled=false`, `api_key_encrypted=null`)
   - `sms_webhook_secret`: cryptographically random 32-byte URL-safe string, logged once
   - `sms_senders`: `["OTP banka", "OTP", "OTP Banka"]`

---

## Phase 2 — SMS Webhook & Auth

6. `POST /api/webhooks/sms`: constant-time compare of `X-Webhook-Secret` header against `settings.sms_webhook_secret`, SHA256 idempotency hash (`from|text|sentStamp`), insert `raw_message` with `parse_status=pending`, enqueue to `Channel<Guid>`, always return 200
7. Auth endpoints: `POST /api/auth/login` (BCrypt, JWT HS256 1h, rate-limited 5/15min/IP via ASP.NET Core middleware), `/refresh` (rotate single-use refresh token — stored hashed, 30d), `/logout` (invalidate token), `/change-password`

---

## Phase 3 — Parsing Pipeline & LLM

8. `SmsProcessingBackgroundService`: dequeues `Channel<Guid>`, on startup re-enqueues all `raw_messages WHERE parse_status='pending'`
9. `OtpBankaSmsParser` class with 3 compiled `Regex` patterns, returns a `ParsedSms` result or null; date `DD.MM.YYYY` + optional `HH:MM`, comma→dot amount, merchant extraction, Namen→notes, transaction type heuristics from Namen keywords (`DVIG`→atm_withdrawal, default→transfer)
10. Merchant normalization: uppercase, strip trailing card digits/hex/phone patterns, strip trailing city+country code from configurable city list
11. Rule lookup (exact match on `merchant_normalized`) → if hit, use category, increment rule stats
12. LLM categorization (only if no rule): call active provider, parse JSON `{category, subcategory, confidence, reasoning}`, resolve/create subcategory, store `merchant_rule` with `created_by=llm`. On failure → Uncategorized, `category_source=default`, no rule stored
13. Three provider implementations in `Infrastructure/Llm/` (can implement in parallel): `OpenAiCategorizationProvider`, `AnthropicCategorizationProvider`, `GeminiCategorizationProvider` — named `IHttpClientFactory` client per provider, Polly retry 3× exponential+jitter 500ms base, log latency/sizes only (never key or prompt body)
14. `LlmProviderResolver`: reads enabled row, caches 60s (invalidated on any provider config change), returns null if none
15. `POST /diagnostic/parse-sms`: dry-run endpoint (no DB write), returns what the regex extracts

---

## Phase 4 — All Remaining API Endpoints

16. **Transactions**: `GET` (paged, all filter params), `GET /{id}`, `POST` (manual), `PATCH /{id}`, `DELETE /{id}` (soft), `POST /{id}/recategorize`, `POST /bulk-recategorize`, `GET /export` (CSV, same filters)
17. **Categories**: `GET`, `POST`, `PATCH /{id}`, `DELETE /{id}` (with `reassignToCategoryId` — reassigns transactions and subcategories' transactions; deletes subcategories of deleted parent). System categories protected.
18. **Merchant rules**: `GET`, `PATCH /{id}`, `DELETE /{id}`
19. **Raw messages**: `GET ?status=failed`, `POST /{id}/reprocess`, `POST /{id}/parse-manual`, `DELETE /{id}`
20. **LLM providers**: `GET`, `GET /{id}`, `PATCH /{id}`, `POST /{id}/enable`, `POST /disable-all`, `POST /{id}/test`, `GET /active` — keys masked everywhere
21. **Analytics** (all 10 endpoints): analytics queries use `CASE WHEN c.parent_category_id IS NOT NULL THEN c.parent_category_id ELSE c.id END` for top-level grouping; subcategory drill-down available on breakdown endpoints; "Spent this year" widget data included in dashboard endpoint
22. **Settings**: `GET /webhook-secret`, `POST /webhook-secret/rotate`, `GET /sms-senders`, `PATCH /sms-senders`
23. **Audit log**: writes on transaction edits, category changes, LLM config changes (no key values)

---

## Phase 5 — Angular Foundation

UI must be **modern and responsive** (mobile-friendly layouts, no fixed-width breakages). Specific color palette is not a priority — use a clean PrimeNG theme as-is.

24. Angular workspace with PrimeNG, `ngx-echarts`, Angular Router
25. Login page, JWT auth guard, token refresh interceptor (auto-retry on 401), error interceptor (PrimeNG `p-toast`)
26. App shell: `p-sidebar` nav, dark mode toggle (CSS custom properties + `localStorage`), layout with router outlet

---

## Phase 6 — Core UI Pages

27. **Dashboard** (`/dashboard`): KPI cards (current month / last 30d / prev 30d / % change), ECharts donut (category breakdown), ECharts line (daily spending last 30d + rolling 7d/30d averages), top merchants table, recent transactions table, **"Spent This Year" widget** (YTD total, avg/day, projected EOY, YoY delta, top 5 categories YTD)
28. **Transactions** (`/transactions`): `p-table` server-side paged, filter sidebar (date range, hierarchical category multi-select, merchant search, amount range, direction, source), inline edit dialog, manual entry dialog, soft delete with confirm, recategorize dialog with "create merchant rule?" toggle, bulk recategorize
29. **Categories** (`/categories`): grid of top-level cards, expand to show subcategories, add/edit dialog (name, color picker, icon picker, parent dropdown for subcategory creation), system categories locked, delete with reassign dialog
30. **Merchant rules** (`/rules`): `p-table`, inline edit of category (hierarchical dropdown), delete
31. **Parse failures** (`/parse-failures`): table of failed raw messages with body preview, reprocess, manual parse dialog

---

## Phase 7 — Reports & Insights

32. **Monthly** (`/reports/monthly`): totals + prev month delta + 3-month rolling avg, per-category comparison table, ECharts bar (daily spend), top merchants list
33. **Yearly** (`/reports/yearly`): YTD totals, month×category grid table, YoY comparison, top 20 largest transactions, ECharts stacked area (category evolution by month)
34. **Insights** (`/reports/insights`): ECharts calendar heatmap (intensity = daily spend, 95th percentile scaled), ECharts bar (day-of-week avg spend + count), recurring transactions list (anomaly flag if amount deviates >20%), first-time merchants this month, quiet days

---

## Phase 8 — LLM & Settings UI

35. `/settings/llm`: 3 provider cards, model `p-dropdown`, API key `p-password` (write-only), `p-inputSwitch` (one active, `p-confirmDialog` before switching), "Test connection" with inline result + latency
36. `/settings/account`: change password form
37. `/settings/webhook`: display/rotate secret (confirm before rotate), SMS sender `p-chips` editor

---

## Phase 9 — Docker Compose

38. Multi-stage `Dockerfile` for API (SDK build → ASP.NET runtime, non-root user)
39. `frontend/Dockerfile` for Angular dev server (Node image, `ng serve --host 0.0.0.0`)
40. `docker-compose.yml`: services `api`, `frontend`, `postgres`, named volumes for DB data and Data Protection keys, `depends_on` with postgres healthcheck
41. `.env.example` documenting all required vars: `INITIAL_PASSWORD`, `JWT_SECRET`, `DB_CONNECTION_STRING`, `DATA_PROTECTION_KEY_PATH`, `ASPNETCORE_ENVIRONMENT`, `FRONTEND_ORIGIN`

---

## Testing

**All code must be fully tested.** This includes:

- **Unit tests** (`UnitTests` project): every service, parser, helper, and business logic component. 100% coverage of all 3 SMS regex patterns, merchant normalization, rule lookup, LLM result parsing, category resolution, and any non-trivial utility logic.
- **Integration tests** (`IntegrationTests` project): all API endpoints tested end-to-end against a real (test) database — auth flows, webhook idempotency, transaction CRUD, analytics query correctness, LLM provider switching. LLM provider calls are always **mocked/stubbed** — no real API calls in tests.
- **Frontend tests**: unit tests for all **services and guards** (not every page component).

`dotnet test` must pass with zero failures. Frontend tests run via `ng test`.

---

## Verification

1. `docker-compose up` → all 3 containers healthy, DB migrated, seed data present
2. POST `/api/webhooks/sms` with Odliv sample → transaction appears with correct amount/direction
3. POST `/api/webhooks/sms` with POS NAKUP sample → `merchant_raw=PSIHOLOGIJA JULIUS`, city stripped
4. Duplicate POST with same SMS → returns `{"status":"duplicate"}`, no second row
5. POST `/api/auth/login` → JWT + refresh token returned
6. GET `/api/analytics/dashboard` → populated JSON with all sections
7. Angular login → dashboard renders with ECharts charts
8. LLM settings → enter key → "Test connection" → success toast
9. `dotnet test` → all unit and integration tests pass
10. `ng test` → all frontend tests pass

---

## Decisions Summary

| Topic | Decision |
|---|---|
| UI | PrimeNG — modern, responsive design; specific color scheme is not a priority |
| Charts | ngx-echarts (Apache ECharts) |
| API key encryption | ASP.NET Core Data Protection, filesystem volume |
| Worker | Hosted in Api process |
| Azure | Excluded |
| Timezone | Excluded (UTC throughout) |
| Subcategories | 2-level max, single `category_id` on transaction pointing to leaf |
| Analytics grouping | Top-level by default, subcategory as drill-down |
| LLM subcategory | LLM suggests both; subcategory auto-created if new |
| Seed subcategories | Mercator/Hofer/Lidl/Spar/Tus → Groceries; Petrol/OMV → Fuel; Netflix/Spotify → Subscriptions |
| ATM withdrawal | Skipped (not used) |
| Card purchase SMS | 3rd regex pattern: `POS NAKUP ...` |

## Excluded

- Azure Container Apps, Key Vault, Postgres Flexible Server
- GitHub Actions CI/CD
- Azure Blob Storage backups
- Europe/Ljubljana timezone conversion (UTC stored and displayed)
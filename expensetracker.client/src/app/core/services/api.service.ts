import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, forkJoin, map, Observable, of, tap } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import {
  AccountSettings,
  AccountSummary,
  AllocationBreakdown,
  BulkRecategorizeRequest,
  Category,
  CreateCategoryRequest,
  CreateTransactionRequest,
  CreateUserRequest,
  DeleteCategoryRequest,
  DashboardAnalytics,
  DashboardStrip,
  HistoryPoint,
  InsightsReport,
  InvestmentDashboardStrip,
  InvestmentHolding,
  InvestmentProvider,
  LlmProvider,
  LlmSettings,
  LlmTestResult,
  ManualAccount,
  ManualParseResult,
  MerchantRule,
  MonthlyReport,
  NarrativeResponse,
  PagedResult,
  PortfolioSummary,
  QueueStatus,
  RawMessage,
  RecentActivity,
  RecategorizeTransactionRequest,
  Transaction,
  TransactionFilters,
  UpdateCategoryRequest,
  UpdateLlmProviderRequest,
  UpdateMerchantRuleRequest,
  UpdateTransactionRequest,
  UserInfo,
  WebhookSettings,
  YearlyReport
} from '../models';
import { SKIP_ERROR_TOAST_CONTEXT } from '../tokens/http-context.tokens';
import { AuthService } from './auth.service';

// Re-export all models for backward compatibility
export * from '../models';

const ACCOUNT_SETTINGS_STORAGE_KEY = 'expense-tracker.account-settings';
const WEBHOOK_SECRET_STORAGE_KEY = 'expense-tracker.webhook-secret';
const SMS_SENDERS_STORAGE_KEY = 'expense-tracker.sms-senders';


@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);

  getTransactions(filters: TransactionFilters = {}): Observable<PagedResult<Transaction>> {
    return this.http.get<PagedResult<Transaction>>(buildApiUrl('/api/transactions'), {
      params: this.createParams(filters)
    });
  }

  getTransaction(id: string): Observable<Transaction> {
    return this.http.get<Transaction>(buildApiUrl(`/api/transactions/${id}`));
  }

  createTransaction(payload: CreateTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>(buildApiUrl('/api/transactions'), payload);
  }

  updateTransaction(id: string, payload: UpdateTransactionRequest): Observable<Transaction> {
    return this.http.patch<Transaction>(buildApiUrl(`/api/transactions/${id}`), payload);
  }

  deleteTransaction(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/transactions/${id}`));
  }

  recategorizeTransaction(id: string, payload: RecategorizeTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>(buildApiUrl(`/api/transactions/${id}/recategorize`), payload);
  }

  bulkRecategorize(payload: BulkRecategorizeRequest): Observable<void> {
    return this.http.post<void>(buildApiUrl('/api/transactions/bulk-recategorize'), payload);
  }

  exportTransactions(filters: TransactionFilters = {}): Observable<Blob> {
    return this.http.get(buildApiUrl('/api/transactions/export'), {
      params: this.createParams(filters),
      responseType: 'blob'
    });
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(buildApiUrl('/api/categories'));
  }

  createCategory(payload: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>(buildApiUrl('/api/categories'), payload);
  }

  updateCategory(id: string, payload: UpdateCategoryRequest): Observable<Category> {
    return this.http.patch<Category>(buildApiUrl(`/api/categories/${id}`), payload);
  }

  deleteCategory(id: string, payload: DeleteCategoryRequest): Observable<void> {
    return this.http.request<void>('DELETE', buildApiUrl(`/api/categories/${id}`), { body: payload });
  }

  getMerchantRules(): Observable<MerchantRule[]> {
    return this.http.get<MerchantRule[]>(buildApiUrl('/api/merchant-rules'));
  }

  updateMerchantRule(id: string, payload: UpdateMerchantRuleRequest): Observable<MerchantRule> {
    return this.http.patch<MerchantRule>(buildApiUrl(`/api/merchant-rules/${id}`), payload);
  }

  deleteMerchantRule(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/merchant-rules/${id}`));
  }

  getRawMessages(status?: string | null): Observable<RawMessage[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<RawMessage[]>(buildApiUrl('/api/raw-messages'), { params });
  }

  reprocessRawMessage(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/raw-messages/${id}/reprocess`), {});
  }

  parseRawMessageManually(text: string): Observable<ManualParseResult> {
    return this.http.post<ManualParseResult>(buildApiUrl('/api/diagnostic/parse-sms'), { text });
  }

  deleteRawMessage(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/raw-messages/${id}`));
  }

  getLlmProviders(): Observable<LlmProvider[]> {
    return this.http.get<LlmProvider[]>(buildApiUrl('/api/llm-providers'));
  }

  getLlmProvider(id: string): Observable<LlmProvider> {
    return this.http.get<LlmProvider>(buildApiUrl(`/api/llm-providers/${id}`));
  }

  getActiveLlmProvider(): Observable<LlmProvider | null> {
    return this.http.get<LlmProvider | null>(buildApiUrl('/api/llm-providers/active'));
  }

  updateLlmProvider(id: string, payload: UpdateLlmProviderRequest): Observable<LlmProvider> {
    return this.http.patch<LlmProvider>(buildApiUrl(`/api/llm-providers/${id}`), payload);
  }

  enableLlmProvider(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/llm-providers/${id}/enable`), {});
  }

  disableAllLlmProviders(): Observable<void> {
    return this.http.post<void>(buildApiUrl('/api/llm-providers/disable-all'), {});
  }

  testLlmProvider(id: string): Observable<LlmTestResult> {
    return this.http.post<LlmTestResult>(buildApiUrl(`/api/llm-providers/${id}/test`), {});
  }

  recategorizeUncategorized(): Observable<{ queuedCount: number }> {
    return this.http.post<{ queuedCount: number }>(buildApiUrl('/api/llm-providers/recategorize-uncategorized'), {});
  }

  getQueueStatus(): Observable<QueueStatus> {
    return this.http.get<QueueStatus>(buildApiUrl('/api/raw-messages/queue-status'));
  }

  getDashboardAnalytics(): Observable<DashboardAnalytics> {
    return this.http.get<DashboardAnalytics>(buildApiUrl('/api/analytics/dashboard'));
  }

  getDashboardStrip(): Observable<DashboardStrip> {
    return this.http.get<DashboardStrip>(buildApiUrl('/api/analytics/dashboard/strip'));
  }

  getDashboardNarrative(): Observable<NarrativeResponse | null> {
    return this.http.get<NarrativeResponse | null>(buildApiUrl('/api/analytics/dashboard/narrative'));
  }

  getMonthlyAnalytics(year: number, month: number): Observable<MonthlyReport> {
    return this.http.get<MonthlyReport>(buildApiUrl('/api/analytics/monthly'), {
      params: this.createParams({ year, month })
    });
  }

  getMonthlyNarrative(year: number, month: number): Observable<NarrativeResponse | null> {
    return this.http.get<NarrativeResponse | null>(buildApiUrl('/api/analytics/monthly/narrative'), {
      params: new HttpParams().set('year', year).set('month', month)
    });
  }

  getYearlyAnalytics(year: number): Observable<YearlyReport> {
    return this.http.get<YearlyReport>(buildApiUrl('/api/analytics/yearly'), {
      params: this.createParams({ year })
    });
  }

  getYearlyNarrative(year: number): Observable<NarrativeResponse | null> {
    return this.http.get<NarrativeResponse | null>(buildApiUrl('/api/analytics/yearly/narrative'), {
      params: new HttpParams().set('year', year)
    });
  }

  regenerateNarratives(): Observable<void> {
    return this.http.post<void>(buildApiUrl('/api/analytics/regenerate-narratives'), {});
  }

  getInsightsAnalytics(): Observable<InsightsReport> {
    return this.http.get<InsightsReport>(buildApiUrl('/api/analytics/insights'));
  }

  getLlmSettings(): Observable<LlmSettings> {
    return forkJoin({
      providers: this.getLlmProviders(),
      activeProvider: this.getActiveLlmProvider()
    });
  }

  getWebhookSecret(): Observable<string> {
    return this.http.get<{ secret: string }>(buildApiUrl('/api/settings/webhook-secret'), {
      context: this.silentContext()
    }).pipe(
      map((response) => response.secret),
      tap((secret) => this.writeStorageValue(WEBHOOK_SECRET_STORAGE_KEY, secret)),
      catchError(() => of(this.readStorageValue(WEBHOOK_SECRET_STORAGE_KEY) ?? this.generateLocalSecret()))
    );
  }

  rotateWebhookSecret(): Observable<{ secret: string }> {
    return this.http.post<{ secret: string }>(buildApiUrl('/api/settings/webhook-secret/rotate'), {}, {
      context: this.silentContext()
    }).pipe(
      tap((response) => this.writeStorageValue(WEBHOOK_SECRET_STORAGE_KEY, response.secret)),
      catchError(() => {
        const secret = this.generateLocalSecret();
        this.writeStorageValue(WEBHOOK_SECRET_STORAGE_KEY, secret);
        return of({ secret });
      })
    );
  }

  getSmsSenders(): Observable<string[]> {
    return this.http.get<string[]>(buildApiUrl('/api/settings/sms-senders'), {
      context: this.silentContext()
    }).pipe(
      map((senders) => this.normalizeSenders(senders)),
      tap((senders) => this.writeStorageValue(SMS_SENDERS_STORAGE_KEY, JSON.stringify(senders))),
      catchError(() => of(this.readStoredSmsSenders()))
    );
  }

  updateSmsSenders(senders: string[]): Observable<string[]> {
    const normalized = this.normalizeSenders(senders);
    return this.http.patch<string[]>(buildApiUrl('/api/settings/sms-senders'), { senders: normalized }, {
      context: this.silentContext()
    }).pipe(
      map((saved) => saved ?? normalized),
      tap((savedSenders) => this.writeStorageValue(SMS_SENDERS_STORAGE_KEY, JSON.stringify(savedSenders))),
      catchError(() => of(normalized))
    );
  }

  getWebhookSettings(): Observable<WebhookSettings> {
    return forkJoin({
      secret: this.getWebhookSecret(),
      senders: this.getSmsSenders()
    });
  }

  getAccountSettings(): Observable<AccountSettings> {
    return this.http.get<AccountSettings>(buildApiUrl('/api/settings/account'), {
      context: this.silentContext()
    }).pipe(
      tap((settings) => this.writeStorageValue(ACCOUNT_SETTINGS_STORAGE_KEY, JSON.stringify(settings))),
      catchError(() => of(this.readStoredAccountSettings()))
    );
  }

  updateAccountSettings(settings: AccountSettings): Observable<AccountSettings> {
    const normalized: AccountSettings = {
      username: settings.username.trim() || (this.authService.getUsername() ?? 'expense-user'),
      defaultCurrency: (settings.defaultCurrency || 'EUR').trim().toUpperCase(),
      emailNotifications: Boolean(settings.emailNotifications),
      webhookNotifications: Boolean(settings.webhookNotifications)
    };

    return this.http.patch<AccountSettings>(buildApiUrl('/api/settings/account'), normalized, {
      context: this.silentContext()
    }).pipe(
      tap((savedSettings) => this.writeStorageValue(ACCOUNT_SETTINGS_STORAGE_KEY, JSON.stringify(savedSettings))),
      catchError(() => {
        this.writeStorageValue(ACCOUNT_SETTINGS_STORAGE_KEY, JSON.stringify(normalized));
        return of(normalized);
      })
    );
  }

  // Admin endpoints
  getUsers(): Observable<UserInfo[]> {
    return this.http.get<UserInfo[]>(buildApiUrl('/api/admin/users'));
  }

  createUser(request: CreateUserRequest): Observable<UserInfo> {
    return this.http.post<UserInfo>(buildApiUrl('/api/admin/users'), request);
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/admin/users/${id}`));
  }

  // Investment API methods
  getInvestmentDashboardStrip(): Observable<InvestmentDashboardStrip> {
    return this.http.get<InvestmentDashboardStrip>(buildApiUrl('/api/investments/dashboard-strip'));
  }

  getPortfolioSummary(): Observable<PortfolioSummary> {
    return this.http.get<PortfolioSummary>(buildApiUrl('/api/investments/summary'));
  }

  getInvestmentAccounts(): Observable<AccountSummary[]> {
    return this.http.get<AccountSummary[]>(buildApiUrl('/api/investments/accounts'));
  }

  getInvestmentHoldings(): Observable<InvestmentHolding[]> {
    return this.http.get<InvestmentHolding[]>(buildApiUrl('/api/investments/holdings'));
  }

  getInvestmentAllocation(type: string = 'accountType'): Observable<AllocationBreakdown> {
    return this.http.get<AllocationBreakdown>(buildApiUrl('/api/investments/allocation'), {
      params: new HttpParams().set('type', type)
    });
  }

  getPortfolioHistory(from?: string, to?: string): Observable<HistoryPoint[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<HistoryPoint[]>(buildApiUrl('/api/investments/history'), { params });
  }

  getInvestmentActivity(limit: number = 50): Observable<RecentActivity[]> {
    return this.http.get<RecentActivity[]>(buildApiUrl('/api/investments/activity'), {
      params: new HttpParams().set('limit', limit)
    });
  }

  getInvestmentNarrative(): Observable<NarrativeResponse | null> {
    return this.http.get<NarrativeResponse | null>(buildApiUrl('/api/investments/narrative'));
  }

  getManualAccounts(): Observable<ManualAccount[]> {
    return this.http.get<ManualAccount[]>(buildApiUrl('/api/investments/manual/accounts'));
  }

  createManualAccount(payload: { displayName: string; accountType: string; currency?: string; initialBalance?: number; icon?: string; color?: string; notes?: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(buildApiUrl('/api/investments/manual/accounts'), payload);
  }

  updateManualAccount(id: string, payload: { displayName?: string; accountType?: string; icon?: string; color?: string; notes?: string; isActive?: boolean }): Observable<void> {
    return this.http.patch<void>(buildApiUrl(`/api/investments/manual/accounts/${id}`), payload);
  }

  deleteManualAccount(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/investments/manual/accounts/${id}`));
  }

  updateManualBalance(accountId: string, payload: { newBalance: number; note?: string }): Observable<{ balance: number }> {
    return this.http.post<{ balance: number }>(buildApiUrl(`/api/investments/manual/accounts/${accountId}/balance`), payload);
  }

  getManualBalanceHistory(accountId: string): Observable<{ balance: number; currency: string; recordedAt: string; note: string | null }[]> {
    return this.http.get<{ balance: number; currency: string; recordedAt: string; note: string | null }[]>(buildApiUrl(`/api/investments/manual/accounts/${accountId}/history`));
  }

  getInvestmentProviders(): Observable<InvestmentProvider[]> {
    return this.http.get<InvestmentProvider[]>(buildApiUrl('/api/investment-providers'));
  }

  updateInvestmentProvider(id: string, payload: { displayName?: string; apiToken?: string; extraConfig?: any }): Observable<void> {
    return this.http.patch<void>(buildApiUrl(`/api/investment-providers/${id}`), payload);
  }

  testInvestmentProvider(id: string): Observable<{ success: boolean; message: string; latencyMs: number }> {
    return this.http.post<{ success: boolean; message: string; latencyMs: number }>(buildApiUrl(`/api/investment-providers/${id}/test`), {});
  }

  enableInvestmentProvider(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/investment-providers/${id}/enable`), {});
  }

  disableInvestmentProvider(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/investment-providers/${id}/disable`), {});
  }

  triggerInvestmentSync(): Observable<{ positions: number; trades: number; warning: string | null }> {
    return this.http.post<{ positions: number; trades: number; warning: string | null }>(buildApiUrl('/api/investments/sync'), {});
  }

  getInvestmentSyncStatus(): Observable<{ configured: boolean; enabled: boolean; lastSyncAt: string | null; lastSyncStatus: string | null; lastSyncError: string | null }> {
    return this.http.get<{ configured: boolean; enabled: boolean; lastSyncAt: string | null; lastSyncStatus: string | null; lastSyncError: string | null }>(buildApiUrl('/api/investments/sync/status'));
  }

  private createParams(values: object): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(values)) {
      if (value === undefined || value === null || value === '') {
        continue;
      }

      if (Array.isArray(value)) {
        for (const item of value) {
          if (item !== undefined && item !== null && item !== '') {
            params = params.append(key, String(item));
          }
        }
        continue;
      }

      params = params.set(key, String(value));
    }

    return params;
  }

  private silentContext(): HttpContext {
    return new HttpContext().set(SKIP_ERROR_TOAST_CONTEXT, true);
  }

  private readStoredAccountSettings(): AccountSettings {
    const parsed = this.parseStorageJson<Partial<AccountSettings>>(this.readStorageValue(ACCOUNT_SETTINGS_STORAGE_KEY));

    return {
      username: parsed?.username ?? this.authService.getUsername() ?? 'expense-user',
      defaultCurrency: parsed?.defaultCurrency ?? 'EUR',
      emailNotifications: parsed?.emailNotifications ?? true,
      webhookNotifications: parsed?.webhookNotifications ?? false
    };
  }

  private readStoredSmsSenders(): string[] {
    const parsed = this.parseStorageJson<string[]>(this.readStorageValue(SMS_SENDERS_STORAGE_KEY));
    return this.normalizeSenders(parsed ?? ['OTP banka', 'OTPBanka']);
  }

  private normalizeSenders(senders: string[]): string[] {
    return Array.from(new Set((senders ?? []).map((sender) => sender.trim()).filter(Boolean)));
  }

  private readStorageValue(key: string): string | null {
    if (typeof localStorage === 'undefined') {
      return null;
    }

    return localStorage.getItem(key);
  }

  private writeStorageValue(key: string, value: string): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    localStorage.setItem(key, value);
  }

  private parseStorageJson<T>(value: string | null): T | null {
    if (!value) {
      return null;
    }

    try {
      return JSON.parse(value) as T;
    } catch {
      return null;
    }
  }

  private generateLocalSecret(): string {
    const cryptoApi = globalThis.crypto;
    if (cryptoApi?.getRandomValues) {
      const bytes = new Uint8Array(24);
      cryptoApi.getRandomValues(bytes);
      return Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
    }

    return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }
}

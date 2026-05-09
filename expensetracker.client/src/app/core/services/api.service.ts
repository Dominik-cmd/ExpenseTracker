import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, forkJoin, map, Observable, of, tap } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import { SKIP_ERROR_TOAST_CONTEXT } from '../tokens/http-context.tokens';
import { AuthService } from './auth.service';

const ACCOUNT_SETTINGS_STORAGE_KEY = 'expense-tracker.account-settings';
const WEBHOOK_SECRET_STORAGE_KEY = 'expense-tracker.webhook-secret';
const SMS_SENDERS_STORAGE_KEY = 'expense-tracker.sms-senders';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Transaction {
  id: string;
  userId: string;
  amount: number;
  currency: string;
  direction: string;
  transactionType: string;
  transactionDate: string;
  merchantRaw?: string | null;
  merchantNormalized?: string | null;
  categoryId: string;
  categorySource: string;
  transactionSource: string;
  notes?: string | null;
  isDeleted: boolean;
  rawMessageId?: string | null;
  createdAt: string;
  updatedAt: string;
  categoryName: string;
  parentCategoryName?: string | null;
}

export interface TransactionFilters {
  from?: string;
  to?: string;
  categoryId?: string;
  categoryIds?: string[];
  merchant?: string;
  minAmount?: number;
  maxAmount?: number;
  direction?: string;
  source?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateTransactionRequest {
  amount: number;
  direction: string;
  transactionType: string;
  transactionDate: string;
  merchantRaw?: string | null;
  categoryId: string;
  notes?: string | null;
}

export interface UpdateTransactionRequest {
  amount?: number | null;
  currency?: string | null;
  direction?: string | null;
  transactionType?: string | null;
  transactionDate?: string | null;
  merchantRaw?: string | null;
  categoryId?: string | null;
  notes?: string | null;
}

export interface RecategorizeTransactionRequest {
  categoryId: string;
  createMerchantRule: boolean;
}

export interface BulkRecategorizeRequest {
  transactionIds: string[];
  categoryId: string;
  createMerchantRule: boolean;
}

export interface Category {
  id: string;
  name: string;
  color?: string | null;
  icon?: string | null;
  sortOrder: number;
  isSystem: boolean;
  excludeFromExpenses: boolean;
  excludeFromIncome: boolean;
  parentCategoryId?: string | null;
  subCategories: Category[];
}

export interface CreateCategoryRequest {
  name: string;
  color?: string | null;
  icon?: string | null;
  parentCategoryId?: string | null;
}

export interface UpdateCategoryRequest {
  name?: string | null;
  color?: string | null;
  icon?: string | null;
  sortOrder?: number | null;
  excludeFromExpenses?: boolean | null;
  excludeFromIncome?: boolean | null;
}

export interface DeleteCategoryRequest {
  reassignToCategoryId: string;
}

export interface MerchantRule {
  id: string;
  merchantNormalized: string;
  categoryId: string;
  categoryName: string;
  parentCategoryName?: string | null;
  createdBy: string;
  hitCount: number;
  lastHitAt?: string | null;
  createdAt: string;
}

export interface UpdateMerchantRuleRequest {
  categoryId: string;
  applyToExistingTransactions?: boolean;
}

export interface RawMessage {
  id: string;
  sender: string;
  body: string;
  receivedAt: string;
  parseStatus: string;
  errorMessage?: string | null;
  idempotencyHash: string;
  transactionId?: string | null;
  createdAt: string;
}

export interface ParsedSmsPreview {
  direction: string;
  transactionType: string;
  amount: number;
  currency: string;
  transactionDate: string;
  merchantRaw: string;
  merchantNormalized: string;
  notes?: string | null;
}

export interface ManualParseResult {
  success: boolean;
  parsedSms: ParsedSmsPreview | null;
  errorMessage?: string | null;
}

export interface LlmProvider {
  id: string;
  providerType: string;
  name: string;
  model: string;
  isEnabled: boolean;
  hasApiKey: boolean;
  lastTestedAt?: string | null;
  lastTestStatus?: string | null;
}

export interface UpdateLlmProviderRequest {
  model?: string | null;
  apiKey?: string | null;
}

export interface LlmTestResult {
  success: boolean;
  latencyMs: number;
  errorMessage?: string | null;
}

export interface DashboardKpi {
  currentMonth: number;
  last30Days: number;
  prev30Days: number;
  percentChange: number;
}

export interface CategoryBreakdown {
  name: string;
  color?: string | null;
  amount: number;
  percentage: number;
}

export interface DailySpending {
  date: string;
  amount: number;
  rolling7Day?: number | null;
  rolling30Day?: number | null;
}

export interface TopMerchant {
  name: string;
  totalAmount: number;
  transactionCount: number;
}

export interface YtdWidget {
  total: number;
  avgPerDay: number;
  projectedEoy: number;
  yoyDelta?: number | null;
  topCategories: CategoryBreakdown[];
}

export interface DashboardAnalytics {
  kpi: DashboardKpi;
  categoryBreakdown: CategoryBreakdown[];
  dailySpending: DailySpending[];
  topMerchants: TopMerchant[];
  recentTransactions: Transaction[];
  ytd: YtdWidget;
  income?: IncomeWidget | null;
}

export interface IncomeWidget {
  currentMonth: number;
  last30Days: number;
  ytdTotal: number;
  netLast30Days: number;
  sources: IncomeSource[];
  monthlyComparison: MonthlyIncomeVsSpending[];
}

export interface IncomeSource {
  name: string;
  totalAmount: number;
  transactionCount: number;
}

export interface MonthlyIncomeVsSpending {
  year: number;
  month: number;
  label: string;
  income: number;
  spending: number;
  net: number;
}

export interface CategoryComparison {
  name: string;
  currentAmount: number;
  previousAmount: number;
  deltaAmount: number;
  deltaPercent: number;
}

export interface MonthlyReport {
  year: number;
  month: number;
  totalAmount: number;
  previousMonthTotal: number;
  percentChange: number;
  rolling3MonthAverage: number;
  categoryComparisons: CategoryComparison[];
  dailySpending: DailySpending[];
  topMerchants: TopMerchant[];
}

export interface MonthlyCategoryTotal {
  month: number;
  categoryName: string;
  amount: number;
}

export interface MonthlyValue {
  month: number;
  amount: number;
}

export interface MonthlyCategorySeries {
  categoryName: string;
  values: MonthlyValue[];
}

export interface YearlyReport {
  year: number;
  yearTotal: number;
  previousYearTotal: number;
  percentChange: number;
  monthlyCategories: MonthlyCategoryTotal[];
  largestTransactions: Transaction[];
  categoryEvolution: MonthlyCategorySeries[];
}

export interface CalendarHeatmapPoint {
  date: string;
  amount: number;
}

export interface DayOfWeekAverage {
  dayOfWeek: string;
  averageAmount: number;
  transactionCount: number;
}

export interface RecurringTransactionInsight {
  merchant: string;
  averageAmount: number;
  latestAmount: number;
  latestDate: string;
  isAnomaly: boolean;
}

export interface FirstTimeMerchantInsight {
  merchant: string;
  amount: number;
  transactionDate: string;
}

export interface InsightsReport {
  calendarHeatmap: CalendarHeatmapPoint[];
  dayOfWeekAverages: DayOfWeekAverage[];
  recurringTransactions: RecurringTransactionInsight[];
  firstTimeMerchants: FirstTimeMerchantInsight[];
  quietDays: string[];
}

export interface WebhookSettings {
  secret: string;
  senders: string[];
}

export interface LlmSettings {
  providers: LlmProvider[];
  activeProvider: LlmProvider | null;
}

export interface AccountSettings {
  username: string;
  defaultCurrency: string;
  emailNotifications: boolean;
  webhookNotifications: boolean;
}

export interface UserInfo {
  id: string;
  username: string;
  isAdmin: boolean;
  createdAt: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  isAdmin: boolean;
}

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

  getDashboardAnalytics(): Observable<DashboardAnalytics> {
    return this.http.get<DashboardAnalytics>(buildApiUrl('/api/analytics/dashboard'));
  }

  getMonthlyAnalytics(year: number, month: number): Observable<MonthlyReport> {
    return this.http.get<MonthlyReport>(buildApiUrl('/api/analytics/monthly'), {
      params: this.createParams({ year, month })
    });
  }

  getYearlyAnalytics(year: number): Observable<YearlyReport> {
    return this.http.get<YearlyReport>(buildApiUrl('/api/analytics/yearly'), {
      params: this.createParams({ year })
    });
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

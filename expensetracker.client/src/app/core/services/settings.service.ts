import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, forkJoin, map, Observable, of, tap } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import {
  AccountSettings,
  LlmLogEntry,
  LlmProvider,
  LlmSettings,
  LlmTestResult,
  PagedResult,
  UpdateLlmProviderRequest,
  WebhookSettings
} from '../models';
import { SKIP_ERROR_TOAST_CONTEXT } from '../tokens/http-context.tokens';
import { AuthService } from './auth.service';

const ACCOUNT_SETTINGS_STORAGE_KEY = 'expense-tracker.account-settings';
const WEBHOOK_SECRET_STORAGE_KEY = 'expense-tracker.webhook-secret';
const SMS_SENDERS_STORAGE_KEY = 'expense-tracker.sms-senders';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);

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
      catchError(() => {
        const cached = this.readStorageValue(WEBHOOK_SECRET_STORAGE_KEY);
        if (cached) {
          return of(cached);
        }
        throw new Error('Failed to load webhook secret');
      })
    );
  }

  rotateWebhookSecret(): Observable<{ secret: string }> {
    return this.http.post<{ secret: string }>(buildApiUrl('/api/settings/webhook-secret/rotate'), {}, {
      context: this.silentContext()
    }).pipe(
      tap((response) => this.writeStorageValue(WEBHOOK_SECRET_STORAGE_KEY, response.secret))
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
      tap((savedSenders) => this.writeStorageValue(SMS_SENDERS_STORAGE_KEY, JSON.stringify(savedSenders)))
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
      tap((savedSettings) => this.writeStorageValue(ACCOUNT_SETTINGS_STORAGE_KEY, JSON.stringify(savedSettings)))
    );
  }

  getLlmLogs(params: { page?: number; pageSize?: number; provider?: string; successOnly?: boolean | null }): Observable<PagedResult<LlmLogEntry>> {
    let httpParams = new HttpParams();

    if (params.page !== undefined) {
      httpParams = httpParams.set('page', params.page);
    }
    if (params.pageSize !== undefined) {
      httpParams = httpParams.set('pageSize', params.pageSize);
    }
    if (params.provider) {
      httpParams = httpParams.set('provider', params.provider);
    }
    if (params.successOnly !== undefined && params.successOnly !== null) {
      httpParams = httpParams.set('successOnly', params.successOnly);
    }

    return this.http.get<PagedResult<LlmLogEntry>>(buildApiUrl('/api/llm-logs'), {
      params: httpParams
    });
  }

  clearLlmLogs(): Observable<void> {
    return this.http.delete<void>(buildApiUrl('/api/llm-logs'));
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
}

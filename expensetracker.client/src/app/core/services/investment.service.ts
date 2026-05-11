import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import {
  AccountSummary,
  AllocationBreakdown,
  HistoryPoint,
  InvestmentDashboardStrip,
  InvestmentHolding,
  InvestmentProvider,
  ManualAccount,
  NarrativeResponse,
  PortfolioSummary,
  RecentActivity
} from '../models';

@Injectable({ providedIn: 'root' })
export class InvestmentService {
  private readonly http = inject(HttpClient);

  getDashboardStrip(): Observable<InvestmentDashboardStrip> {
    return this.http.get<InvestmentDashboardStrip>(buildApiUrl('/api/investments/dashboard-strip'));
  }

  getPortfolioSummary(): Observable<PortfolioSummary> {
    return this.http.get<PortfolioSummary>(buildApiUrl('/api/investments/summary'));
  }

  getAccounts(): Observable<AccountSummary[]> {
    return this.http.get<AccountSummary[]>(buildApiUrl('/api/investments/accounts'));
  }

  getHoldings(): Observable<InvestmentHolding[]> {
    return this.http.get<InvestmentHolding[]>(buildApiUrl('/api/investments/holdings'));
  }

  getAllocation(type = 'accountType'): Observable<AllocationBreakdown> {
    return this.http.get<AllocationBreakdown>(buildApiUrl('/api/investments/allocation'), {
      params: new HttpParams().set('type', type)
    });
  }

  getPortfolioHistory(from?: string, to?: string): Observable<HistoryPoint[]> {
    let params = new HttpParams();
    if (from) {
      params = params.set('from', from);
    }
    if (to) {
      params = params.set('to', to);
    }
    return this.http.get<HistoryPoint[]>(buildApiUrl('/api/investments/history'), { params });
  }

  getActivity(limit = 50): Observable<RecentActivity[]> {
    return this.http.get<RecentActivity[]>(buildApiUrl('/api/investments/activity'), {
      params: new HttpParams().set('limit', limit)
    });
  }

  getNarrative(): Observable<NarrativeResponse | null> {
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

  getProviders(): Observable<InvestmentProvider[]> {
    return this.http.get<InvestmentProvider[]>(buildApiUrl('/api/investment-providers'));
  }

  updateProvider(id: string, payload: { displayName?: string; apiToken?: string; extraConfig?: unknown }): Observable<void> {
    return this.http.patch<void>(buildApiUrl(`/api/investment-providers/${id}`), payload);
  }

  testProvider(id: string): Observable<{ success: boolean; message: string; latencyMs: number }> {
    return this.http.post<{ success: boolean; message: string; latencyMs: number }>(buildApiUrl(`/api/investment-providers/${id}/test`), {});
  }

  enableProvider(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/investment-providers/${id}/enable`), {});
  }

  disableProvider(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/investment-providers/${id}/disable`), {});
  }

  triggerSync(): Observable<{ positions: number; trades: number; warning: string | null }> {
    return this.http.post<{ positions: number; trades: number; warning: string | null }>(buildApiUrl('/api/investments/sync'), {});
  }

  getSyncStatus(): Observable<{ configured: boolean; enabled: boolean; lastSyncAt: string | null; lastSyncStatus: string | null; lastSyncError: string | null }> {
    return this.http.get<{ configured: boolean; enabled: boolean; lastSyncAt: string | null; lastSyncStatus: string | null; lastSyncError: string | null }>(buildApiUrl('/api/investments/sync/status'));
  }
}

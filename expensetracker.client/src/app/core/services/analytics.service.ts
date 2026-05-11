import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import {
  DashboardAnalytics,
  DashboardStrip,
  InsightsReport,
  MonthlyReport,
  NarrativeResponse,
  YearlyReport
} from '../models';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);

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

  private createParams(values: object): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(values)) {
      if (value === undefined || value === null || value === '') continue;
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
}

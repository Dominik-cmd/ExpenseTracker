import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { CardModule } from 'primeng/card';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import {
  ApiService,
  DashboardAnalytics,
  DashboardStrip,
  NarrativeResponse,
  Transaction
} from '../../core/services/api.service';

const STRIP_FALLBACK: DashboardStrip = {
  monthToDate: 0,
  onPace: 0,
  netLast30: 0,
  netLast30Income: 0,
  netLast30Spending: 0
};

const DASHBOARD_FALLBACK: DashboardAnalytics = {
  categoryLeaderboard: [],
  recentTransactions: [],
  income: null
};

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, CardModule, NgxEchartsDirective, RouterLink],
  template: `
    <div class="dashboard-root">
      <div class="widget-card summary-card">
        <div class="strip-numbers">
          <span class="strip-item">
            <span class="strip-label">This month</span>
            <span class="strip-value">{{ strip().monthToDate | currency:'EUR':'symbol':'1.0-0' }}</span>
          </span>
          <span class="strip-sep">·</span>
          <span class="strip-item">
            <span class="strip-label">On pace</span>
            <span class="strip-value">{{ strip().onPace | currency:'EUR':'symbol':'1.0-0' }}</span>
          </span>
          <span class="strip-sep">·</span>
          <span class="strip-item">
            <span class="strip-label">Net 30d</span>
            <span class="strip-value" [class.text-positive]="strip().netLast30 > 0" [class.text-negative]="strip().netLast30 < 0">
              {{ strip().netLast30 > 0 ? '+' : '' }}{{ strip().netLast30 | currency:'EUR':'symbol':'1.0-0' }}
            </span>
          </span>
        </div>
        @if (narrative()?.content) {
          <div class="strip-narrative">{{ narrative()!.content }}</div>
        }
        @if (narrative()?.isStale) {
          <div class="narrative-stale">updating...</div>
        }
      </div>

      <div class="main-grid">
        <div class="widget-card flex-card">
          <div class="widget-header">
            <span class="widget-title">Recent transactions</span>
            <a class="see-all-link" routerLink="/transactions">See all →</a>
          </div>
          @if ((analytics()?.recentTransactions?.length ?? 0) > 0) {
            <div class="txn-list">
              @for (t of (analytics()?.recentTransactions ?? []).slice(0, 8); track t.id) {
                <div class="txn-row">
                  <span class="txn-dot" [style.background]="getCategoryColor(t)"></span>
                  <span class="txn-date">{{ t.transactionDate | date:'dd MMM' }}</span>
                  <span class="txn-merchant">{{ (t.merchantNormalized || t.merchantRaw || 'Manual entry') | slice:0:28 }}</span>
                  <span class="txn-amount" [class.text-positive]="t.direction === 'Credit'" [class.text-negative]="t.direction !== 'Credit'">
                    {{ t.direction === 'Credit' ? '+' : '−' }}{{ abs(t.amount) | currency:'EUR':'symbol':'1.2-2' }}
                  </span>
                </div>
              }
            </div>
          } @else {
            <div class="empty-state">No transactions yet. Send a test SMS to verify the webhook.</div>
          }
        </div>

        <div class="widget-card flex-card">
          <div class="widget-header">
            <div>
              <div class="widget-title">Spending this month</div>
              <div class="widget-subtitle">Top 8 categories</div>
            </div>
            <span class="leaderboard-total">{{ leaderboardTotal() | currency:'EUR':'symbol':'1.0-0' }}</span>
          </div>
          @if (leaderboard().length > 0) {
            <div class="leaderboard-list">
              @for (item of leaderboard(); track item.name) {
                <div class="leaderboard-row">
                  <div class="lb-top-line">
                    <span class="lb-dot" [style.background]="item.color || '#94a3b8'"></span>
                    <span class="lb-name">{{ item.name }}</span>
                    <span class="lb-amount">{{ item.amount >= 100 ? (item.amount | currency:'EUR':'symbol':'1.0-0') : (item.amount | currency:'EUR':'symbol':'1.2-2') }}</span>
                    <span class="lb-pct">{{ item.percentage.toFixed(1) }}%</span>
                  </div>
                  <div class="lb-bar-track">
                    <div class="lb-bar-fill" [style.width.%]="(item.amount / leaderboardMax()) * 100" [style.background]="barColor(item.color)"></div>
                  </div>
                </div>
              }
            </div>
          } @else {
            <div class="empty-state">No spending recorded yet this month.</div>
          }
        </div>
      </div>

      <div class="widget-card">
        <div class="widget-header">
          <span class="widget-title">Income vs Spending</span>
          <span class="widget-subtitle">Last 6 months</span>
        </div>
        <div echarts [options]="incomeVsSpendingOptions()" class="income-chart"></div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-root {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    .summary-card {
      /* inherits widget-card box; just stacks vertically */
    }

    .strip-numbers {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.5rem 1rem;
      font-size: 1rem;
    }

    .strip-item {
      display: inline-flex;
      align-items: baseline;
      gap: 0.4rem;
    }

    .strip-label {
      font-size: 0.8rem;
      color: var(--text-color-secondary);
      text-transform: lowercase;
    }

    .strip-value {
      font-size: 1.25rem;
      font-weight: 600;
      color: var(--text-color);
    }

    .strip-sep {
      color: var(--text-color-secondary);
      font-size: 1.1rem;
    }

    .strip-narrative {
      margin-top: 0.5rem;
      font-size: 0.9rem;
      font-style: italic;
      color: var(--text-color-secondary);
      line-height: 1.4;
    }

    .narrative-stale {
      font-size: 0.72rem;
      color: var(--yellow-500);
      margin-top: 0.2rem;
    }

    .main-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.25rem;
      align-items: stretch;
    }

    @media (max-width: 768px) {
      .main-grid {
        grid-template-columns: 1fr;
      }
    }

    .widget-card {
      background: var(--surface-card);
      border: 1px solid var(--surface-border);
      border-radius: 12px;
      padding: 1.25rem 1.5rem;
    }

    .flex-card {
      display: flex;
      flex-direction: column;
    }

    .widget-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1rem;
    }

    .widget-title {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-color);
    }

    .widget-subtitle {
      font-size: 0.8rem;
      color: var(--text-color-secondary);
      margin-top: 0.15rem;
    }

    .see-all-link {
      font-size: 0.8rem;
      color: var(--primary-color);
      text-decoration: none;
      white-space: nowrap;
    }

    .see-all-link:hover {
      text-decoration: underline;
    }

    .leaderboard-total {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-color-secondary);
    }

    .txn-list {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0;
    }

    .txn-row {
      display: grid;
      grid-template-columns: 10px 52px 1fr auto;
      align-items: center;
      gap: 0.6rem;
      padding: 0.55rem 0;
      border-bottom: 1px solid var(--surface-border);
    }

    .txn-row:last-child {
      border-bottom: none;
    }

    .txn-dot {
      width: 9px;
      height: 9px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .txn-date {
      font-size: 0.78rem;
      color: var(--text-color-secondary);
      white-space: nowrap;
    }

    .txn-merchant {
      font-size: 0.875rem;
      color: var(--text-color);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .txn-amount {
      font-size: 0.875rem;
      font-weight: 500;
      white-space: nowrap;
      text-align: right;
    }

    .leaderboard-list {
      flex: 1;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: 0.65rem;
    }

    .leaderboard-row {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
    }

    .lb-top-line {
      display: grid;
      grid-template-columns: 12px 1fr auto auto;
      align-items: center;
      gap: 0.5rem;
    }

    .lb-dot {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .lb-name {
      font-size: 0.875rem;
      color: var(--text-color);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .lb-amount {
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--text-color);
      white-space: nowrap;
    }

    .lb-pct {
      font-size: 0.75rem;
      color: var(--text-color-secondary);
      white-space: nowrap;
      min-width: 36px;
      text-align: right;
    }

    .lb-bar-track {
      height: 4px;
      background: var(--surface-ground);
      border-radius: 2px;
      overflow: hidden;
      margin-left: 20px;
    }

    .lb-bar-fill {
      height: 100%;
      border-radius: 2px;
      transition: width 0.3s;
    }

    .income-chart {
      width: 100%;
      height: 20rem;
    }

    .text-positive {
      color: #10b981;
    }

    .text-negative {
      color: #ef4444;
    }

    .empty-state {
      padding: 1.5rem 0;
      text-align: center;
      color: var(--text-color-secondary);
      font-size: 0.875rem;
    }
  `]
})
export class DashboardComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly euroFormatter = new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  });

  protected readonly strip = signal<DashboardStrip>(STRIP_FALLBACK);
  protected readonly analytics = signal<DashboardAnalytics | null>(null);
  protected readonly narrative = signal<NarrativeResponse | null>(null);

  protected readonly leaderboard = computed(() => {
    const items = [...(this.analytics()?.categoryLeaderboard ?? [])].sort((a, b) => b.amount - a.amount);
    const top8 = items.slice(0, 8);
    const rest = items.slice(8);

    if (rest.length > 0) {
      top8.push({
        name: `Other (${rest.length} categories)`,
        color: '#94a3b8',
        amount: rest.reduce((sum, item) => sum + item.amount, 0),
        percentage: rest.reduce((sum, item) => sum + item.percentage, 0)
      });
    }

    return top8;
  });

  protected readonly leaderboardTotal = computed(() =>
    (this.analytics()?.categoryLeaderboard ?? []).reduce((sum, item) => sum + item.amount, 0)
  );

  protected readonly leaderboardMax = computed(() => {
    const items = this.leaderboard();
    return items.length > 0 ? items[0].amount : 1;
  });

  protected readonly incomeVsSpendingOptions = computed<EChartsOption>(() => {
    const months = this.analytics()?.income?.monthlyComparison ?? [];

    return {
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      legend: { top: 0, data: ['Income', 'Spending'] },
      grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
      xAxis: { type: 'category', data: months.map((month) => month.label) },
      yAxis: {
        type: 'value',
        axisLabel: {
          formatter: (value: number) => this.euroFormatter.format(value)
        }
      },
      series: [
        {
          name: 'Income',
          type: 'bar',
          data: months.map((month) => month.income),
          itemStyle: { color: '#10b981' },
          barMaxWidth: 36
        },
        {
          name: 'Spending',
          type: 'bar',
          data: months.map((month) => month.spending),
          itemStyle: { color: '#ef4444' },
          barMaxWidth: 36
        }
      ]
    };
  });

  constructor() {
    forkJoin({
      strip: this.apiService.getDashboardStrip().pipe(catchError(() => of(STRIP_FALLBACK))),
      analytics: this.apiService.getDashboardAnalytics().pipe(catchError(() => of(DASHBOARD_FALLBACK))),
      narrative: this.apiService.getDashboardNarrative().pipe(catchError(() => of(null)))
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ strip, analytics, narrative }) => {
      this.strip.set(strip);
      this.analytics.set(analytics);
      this.narrative.set(narrative);
    });
  }

  protected abs(value: number | null | undefined): number {
    return Math.abs(value ?? 0);
  }

  protected getCategoryColor(transaction: Transaction): string {
    const leaderboard = this.analytics()?.categoryLeaderboard ?? [];
    const match = leaderboard.find((item) => item.name === (transaction.parentCategoryName || transaction.categoryName));
    return match?.color || '#94a3b8';
  }

  protected barColor(hex: string | null | undefined): string {
    if (!hex || hex.length < 6) {
      return 'rgba(148,163,184,0.35)';
    }

    const normalized = hex.replace('#', '');
    if (normalized.length !== 6) {
      return 'rgba(148,163,184,0.35)';
    }

    const r = parseInt(normalized.substring(0, 2), 16);
    const g = parseInt(normalized.substring(2, 4), 16);
    const b = parseInt(normalized.substring(4, 6), 16);

    return `rgba(${r}, ${g}, ${b}, 0.35)`;
  }
}

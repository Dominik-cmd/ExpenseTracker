import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import { ApiService, DashboardAnalytics, NarrativeResponse, Transaction } from '../../core/services/api.service';

const DASHBOARD_FALLBACK: DashboardAnalytics = {
  kpi: {
    currentMonth: 0,
    last30Days: 0,
    prev30Days: 0,
    percentChange: 0,
    projectedMonthEnd: 0,
    sameMonthLastYear: null,
    netFlow30d: 0,
    income30d: 0,
    spending30d: 0
  },
  categoryBreakdown: [],
  categoryComparisons: [],
  dailySpending: [],
  topMerchants: [],
  recentTransactions: [],
  ytd: { total: 0, avgPerDay: 0, projectedEoy: 0, yoyDelta: null, topCategories: [] },
  income: null
};

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, ButtonModule, CardModule, TableModule, NgxEchartsDirective],
  template: `
    <div class="grid dashboard-grid">
      @if (narrative()?.content) {
        <div class="col-12">
          <div class="narrative-card">
            <div class="narrative-content">{{ narrative()!.content }}</div>
            <div class="narrative-meta">
              <i class="pi pi-sparkles" style="font-size: 0.7rem"></i>
              <span>AI-generated summary</span>
              @if (narrative()!.isStale) {
                <span class="narrative-stale">· updating...</span>
              }
            </div>
          </div>
        </div>
      }

      <div class="col-12 md:col-6">
        <div class="hero-tile h-full">
          <div class="text-sm text-color-secondary">This month projection</div>
          <div class="hero-headline mt-2">{{ kpi().currentMonth | currency:'EUR':'symbol':'1.0-0' }}</div>
          <div class="hero-sub">On pace for {{ kpi().projectedMonthEnd | currency:'EUR':'symbol':'1.0-0' }} by month-end</div>
          <div class="hero-sub">
            @if (kpi().sameMonthLastYear !== null && kpi().sameMonthLastYear !== undefined) {
              <span>Last year: {{ kpi().sameMonthLastYear | currency:'EUR':'symbol':'1.0-0' }}</span>
            } @else {
              <span>No data for last year</span>
            }
          </div>
        </div>
      </div>

      <div class="col-12 md:col-6">
        <div class="hero-tile h-full">
          <div class="text-sm text-color-secondary">Net flow (30d)</div>
          <div
            class="hero-headline mt-2"
            [class.text-positive]="kpi().netFlow30d > 0"
            [class.text-negative]="kpi().netFlow30d < 0">
            {{ kpi().netFlow30d > 0 ? '+' : kpi().netFlow30d < 0 ? '−' : '' }}{{ abs(kpi().netFlow30d) | currency:'EUR':'symbol':'1.0-0' }}
          </div>
          <div class="hero-sub">
            {{ kpi().income30d | currency:'EUR':'symbol':'1.0-0' }} in / {{ kpi().spending30d | currency:'EUR':'symbol':'1.0-0' }} out
          </div>
        </div>
      </div>

      <div class="col-12 xl:col-6 primary-widget">
        <p-card header="Spending by category" subheader="Top categories this month">
          <div echarts [options]="breakdownOptions()" class="chart category-chart"></div>
        </p-card>
      </div>

      <div class="col-12 xl:col-6 primary-widget">
        <p-card header="What changed" subheader="Current month vs previous month">
          @if (categoryComparisons().length) {
            <div class="comparison-table-wrap">
              <table class="comparison-table">
                <thead>
                  <tr>
                    <th>Category</th>
                    <th class="text-right">Current</th>
                    <th class="text-right">Previous</th>
                    <th class="text-right">Delta</th>
                  </tr>
                </thead>
                <tbody>
                  @for (comparison of categoryComparisons(); track comparison.name) {
                    <tr>
                      <td class="font-medium">{{ comparison.name }}</td>
                      <td class="text-right">{{ comparison.currentAmount | currency:'EUR':'symbol':'1.0-0' }}</td>
                      <td class="text-right">{{ comparison.previousAmount | currency:'EUR':'symbol':'1.0-0' }}</td>
                      <td class="text-right">
                        <div
                          [class.text-negative]="comparison.deltaAmount > 0"
                          [class.text-positive]="comparison.deltaAmount < 0">
                          {{ comparison.deltaAmount > 0 ? '+' : '' }}{{ comparison.deltaAmount | currency:'EUR':'symbol':'1.0-0' }}
                        </div>
                        <div
                          class="comparison-delta-percent"
                          [class.text-negative]="comparison.deltaAmount > 0"
                          [class.text-positive]="comparison.deltaAmount < 0">
                          {{ formatPercent(comparison.deltaPercent) }}
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="empty-state">No month-over-month category changes available yet.</div>
          }
        </p-card>
      </div>

      <div class="col-12 xl:col-7 primary-widget">
        <p-card header="Income vs Spending" subheader="Last 6 months comparison">
          <div echarts [options]="incomeVsSpendingOptions()" class="chart"></div>
        </p-card>
      </div>

      <div class="col-12 xl:col-5 secondary-widget">
        <p-card header="Spending trend" subheader="Daily spend with rolling averages">
          <div class="flex gap-2 mb-2">
            <p-button [label]="'Show all'" [outlined]="excludeOutliers()" size="small" (onClick)="excludeOutliers.set(false)"></p-button>
            <p-button [label]="'Exclude outliers > €' + outlierThreshold()" [outlined]="!excludeOutliers()" size="small" (onClick)="excludeOutliers.set(true)"></p-button>
          </div>
          <div echarts [options]="trendOptions()" class="chart"></div>
        </p-card>
      </div>

      <div class="col-12 xl:col-5 secondary-widget">
        <p-card header="Top merchants">
          @if ((analytics()?.topMerchants?.length ?? 0) > 0) {
            <p-table [value]="analytics()?.topMerchants ?? []" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr>
                  <th>Merchant</th>
                  <th class="text-right">Txns</th>
                  <th class="text-right">Total</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-merchant>
                <tr>
                  <td class="font-medium">{{ merchant.name }}</td>
                  <td class="text-right">{{ merchant.transactionCount }}</td>
                  <td class="text-right">{{ merchant.totalAmount | currency:'EUR':'symbol':'1.2-2' }}</td>
                </tr>
              </ng-template>
            </p-table>
          } @else {
            <div class="empty-state text-center text-color-secondary">
              <i class="pi pi-info-circle"></i>
              <span>No merchant data yet. Transactions will appear as they're processed.</span>
            </div>
          }
        </p-card>
      </div>

      <div class="col-12 xl:col-7 secondary-widget">
        <p-card header="Recent transactions">
          @if ((analytics()?.recentTransactions?.length ?? 0) > 0) {
            <p-table [value]="analytics()?.recentTransactions ?? []" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr>
                  <th style="width: 24px;"></th>
                  <th>Date</th>
                  <th>Merchant</th>
                  <th>Category</th>
                  <th class="text-right">Amount</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-t>
                <tr>
                  <td style="width: 24px; padding-right: 0;">
                    <span
                      class="category-dot"
                      [style.background]="getCategoryColor(t)"></span>
                  </td>
                  <td>{{ t.transactionDate | date:'dd.MM.yy' }}</td>
                  <td>{{ t.merchantNormalized || t.merchantRaw || 'Manual entry' }}</td>
                  <td>{{ t.parentCategoryName || t.categoryName }}</td>
                  <td
                    class="text-right font-medium"
                    [class.text-positive]="t.direction === 'Credit'"
                    [class.text-negative]="t.direction !== 'Credit'">
                    {{ t.direction === 'Credit' ? '+' : '−' }}{{ abs(t.amount) | currency:'EUR':'symbol':'1.2-2' }}
                  </td>
                </tr>
              </ng-template>
            </p-table>
          } @else {
            <div class="empty-state text-center text-color-secondary">
              <i class="pi pi-info-circle"></i>
              <span>No transactions yet. Send a test SMS to verify the webhook.</span>
            </div>
          }
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-grid {
      row-gap: 1.25rem;
    }

    .chart {
      width: 100%;
      height: 22rem;
    }

    .category-chart {
      height: 24rem;
    }

    .narrative-card {
      background: color-mix(in srgb, var(--primary-color) 5%, var(--surface-card));
      border: 1px solid color-mix(in srgb, var(--primary-color) 20%, var(--surface-border));
      border-radius: 12px;
      padding: 1.25rem 1.5rem;
      margin-bottom: 0.5rem;
    }

    .narrative-content {
      font-size: 1.05rem;
      line-height: 1.6;
      font-style: italic;
      color: var(--text-color);
    }

    .narrative-meta {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      margin-top: 0.75rem;
      font-size: 0.7rem;
      color: var(--text-color-secondary);
    }

    .narrative-stale {
      color: var(--yellow-500);
    }

    .hero-tile {
      background: linear-gradient(135deg, var(--surface-card) 0%, color-mix(in srgb, var(--primary-color) 4%, var(--surface-card)) 100%);
      border: 1px solid color-mix(in srgb, var(--primary-color) 15%, var(--surface-border));
      border-radius: 12px;
      padding: 1.75rem;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
    }

    .hero-headline {
      font-size: 2.75rem;
      font-weight: 700;
      line-height: 1.1;
      letter-spacing: -0.02em;
    }

    .hero-sub {
      font-size: 0.875rem;
      color: var(--text-color-secondary);
      margin-top: 0.4rem;
    }

    :host ::ng-deep .primary-widget .p-card {
      border-radius: 12px;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.03);
    }

    :host ::ng-deep .primary-widget .p-card-title {
      font-size: 1.1rem;
      font-weight: 600;
    }

    :host ::ng-deep .secondary-widget .p-card {
      border-radius: 10px;
      border: 1px solid var(--surface-border);
      box-shadow: none;
    }

    :host ::ng-deep .secondary-widget .p-card-title {
      font-size: 0.95rem;
      font-weight: 500;
      color: var(--text-color-secondary);
    }

    .text-positive {
      color: #10b981;
    }

    .text-negative {
      color: #ef4444;
    }

    .category-dot {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 50%;
    }

    .comparison-table-wrap {
      overflow-x: auto;
    }

    .comparison-table {
      width: 100%;
      border-collapse: collapse;
    }

    .comparison-table th,
    .comparison-table td {
      padding: 0.8rem 0;
      border-bottom: 1px solid var(--surface-border);
      vertical-align: top;
    }

    .comparison-table th {
      color: var(--text-color-secondary);
      font-size: 0.875rem;
      font-weight: 600;
    }

    .comparison-delta-percent {
      font-size: 0.85rem;
      margin-top: 0.2rem;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      color: var(--text-color-secondary);
      padding: 1.5rem 0.75rem;
    }

    .empty-state i {
      font-size: 1rem;
      opacity: 0.8;
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

  protected readonly analytics = signal<DashboardAnalytics | null>(null);
  protected readonly narrative = signal<NarrativeResponse | null>(null);
  protected readonly excludeOutliers = signal(false);
  protected readonly kpi = computed(() => this.analytics()?.kpi ?? DASHBOARD_FALLBACK.kpi);
  protected readonly categoryComparisons = computed(() =>
    [...(this.analytics()?.categoryComparisons ?? DASHBOARD_FALLBACK.categoryComparisons)]
      .sort((a, b) => Math.abs(b.deltaAmount) - Math.abs(a.deltaAmount))
  );
  protected readonly outlierThreshold = computed(() => {
    const series = this.analytics()?.dailySpending ?? [];
    if (series.length === 0) return 0;
    const amounts = series.map(s => s.amount).filter(a => a > 0).sort((a, b) => a - b);
    if (amounts.length === 0) return 0;
    const index = Math.floor(amounts.length * 0.95);
    return amounts[Math.min(index, amounts.length - 1)];
  });

  protected readonly breakdownOptions = computed<EChartsOption>(() => {
    const breakdown = [...(this.analytics()?.categoryBreakdown ?? [])].sort((a, b) => b.amount - a.amount);
    const top8 = breakdown.slice(0, 8);
    const rest = breakdown.slice(8);
    const items = [...top8];

    if (rest.length > 0) {
      items.push({
        name: `Other (${rest.length} categories)`,
        color: '#94a3b8',
        amount: rest.reduce((sum, item) => sum + item.amount, 0),
        percentage: rest.reduce((sum, item) => sum + item.percentage, 0)
      });
    }

    const reversed = [...items].reverse();

    return {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        formatter: (params: unknown) => {
          const point = Array.isArray(params) ? params[0] as { dataIndex: number } : params as { dataIndex: number };
          const item = reversed[point?.dataIndex ?? 0];
          return item
            ? `${item.name}: ${this.euroFormatter.format(item.amount)} (${item.percentage.toFixed(1)}%)`
            : '';
        }
      },
      grid: { left: 8, right: 180, top: 8, bottom: 8, containLabel: true },
      xAxis: {
        type: 'value',
        axisLabel: {
          formatter: (value: number) => this.euroFormatter.format(value)
        }
      },
      yAxis: {
        type: 'category',
        data: reversed.map((item) => item.name),
        axisLabel: { fontSize: 12 }
      },
      series: [{
        type: 'bar',
        data: reversed.map((item) => ({
          value: item.amount,
          itemStyle: { color: item.color || '#94a3b8' }
        })),
        barMaxWidth: 28,
        label: {
          show: true,
          position: 'right',
          fontSize: 11,
          formatter: (params: unknown) => {
            const point = params as { dataIndex: number };
            const item = reversed[point?.dataIndex ?? 0];
            return item
              ? `${item.name} · ${this.euroFormatter.format(item.amount)} · ${item.percentage.toFixed(1)}%`
              : '';
          }
        }
      }]
    };
  });

  protected readonly trendOptions = computed<EChartsOption>(() => {
    const series = this.analytics()?.dailySpending ?? DASHBOARD_FALLBACK.dailySpending;
    const threshold = this.outlierThreshold();
    const excludeMode = this.excludeOutliers();
    const labels = series.map((item) => new Date(item.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));

    const outlierIndices = new Set<number>();
    if (excludeMode && threshold > 0) {
      series.forEach((item, idx) => {
        if (item.amount > threshold) {
          outlierIndices.add(idx);
        }
      });
    }

    const dailyData = series.map((item, idx) => outlierIndices.has(idx) ? null : item.amount);

    const rolling7d = series.map((_, idx) => {
      const windowStart = Math.max(0, idx - 6);
      let sum = 0;
      let count = 0;

      for (let i = windowStart; i <= idx; i++) {
        if (!outlierIndices.has(i)) {
          sum += series[i].amount;
          count++;
        }
      }

      return count > 0 ? Math.round(sum / count * 100) / 100 : null;
    });

    const rolling30d = series.map((_, idx) => {
      const windowStart = Math.max(0, idx - 29);
      let sum = 0;
      let count = 0;

      for (let i = windowStart; i <= idx; i++) {
        if (!outlierIndices.has(i)) {
          sum += series[i].amount;
          count++;
        }
      }

      return count > 0 ? Math.round(sum / count * 100) / 100 : null;
    });

    const markPointData = excludeMode
      ? series
        .map((item, idx) => outlierIndices.has(idx)
          ? {
              coord: [idx, threshold * 0.95],
              value: `€${Math.round(item.amount)}`,
              symbol: 'pin',
              symbolSize: 40,
              label: { show: true, fontSize: 10 },
              itemStyle: { color: '#ef4444' }
            }
          : null)
        .filter(Boolean)
      : [];

    return {
      tooltip: { trigger: 'axis' },
      legend: { top: 0 },
      grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
      xAxis: { type: 'category', data: labels },
      yAxis: {
        type: 'value',
        axisLabel: {
          formatter: (value: number) => this.euroFormatter.format(value)
        }
      },
      series: [
        {
          name: 'Daily spend',
          type: 'line',
          smooth: true,
          data: excludeMode ? dailyData : series.map((item) => item.amount),
          lineStyle: { color: '#ef4444' },
          itemStyle: { color: '#ef4444' },
          markPoint: markPointData.length > 0 ? { data: markPointData as any } : undefined
        },
        {
          name: 'Rolling 7d',
          type: 'line',
          smooth: true,
          data: excludeMode ? rolling7d : series.map((item) => item.rolling7Day ?? item.amount),
          lineStyle: { color: '#f59e0b' },
          itemStyle: { color: '#f59e0b' }
        },
        {
          name: 'Rolling 30d',
          type: 'line',
          smooth: true,
          data: excludeMode ? rolling30d : series.map((item) => item.rolling30Day ?? item.amount),
          lineStyle: { color: '#3b82f6' },
          itemStyle: { color: '#3b82f6' }
        }
      ]
    };
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
    this.apiService.getDashboardAnalytics().pipe(
      catchError(() => of(DASHBOARD_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((analytics) => this.analytics.set(analytics));

    this.apiService.getDashboardNarrative().pipe(
      catchError(() => of(null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((narrative) => this.narrative.set(narrative));
  }

  protected abs(value: number | null | undefined): number {
    return Math.abs(value ?? 0);
  }

  protected formatPercent(value: number): string {
    if (!Number.isFinite(value)) {
      return '—';
    }

    const abs = Math.abs(value);
    const sign = value > 0 ? '+' : value < 0 ? '-' : '';
    const formatted = abs >= 10 ? Math.round(abs).toString() : abs.toFixed(1);
    return `${sign}${formatted}%`;
  }

  protected getCategoryColor(t: Transaction): string {
    const breakdown = this.analytics()?.categoryBreakdown ?? [];
    const match = breakdown.find((b) => b.name === (t.parentCategoryName || t.categoryName));
    return match?.color || '#94a3b8';
  }
}

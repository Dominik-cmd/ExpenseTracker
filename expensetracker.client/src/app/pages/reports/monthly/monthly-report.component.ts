import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import { ApiService, MonthlyReport, NarrativeResponse } from '../../../core/services/api.service';

const now = new Date();
const MONTHLY_FALLBACK: MonthlyReport = {
  year: now.getFullYear(),
  month: now.getMonth() + 1,
  totalAmount: 0,
  previousMonthTotal: 0,
  percentChange: 0,
  rolling3MonthAverage: 0,
  categoryComparisons: [],
  dailySpending: [],
  topMerchants: [],
  dailyCategoryBreakdown: []
};

@Component({
  standalone: true,
  selector: 'app-monthly-report',
  imports: [CommonModule, FormsModule, CardModule, InputTextModule, TableModule, NgxEchartsDirective],
  template: `
    <div class="flex flex-column gap-4">
      @if (narrative()?.content) {
        <div class="narrative-card">
          <div class="narrative-content">{{ narrative()!.content }}</div>
          @if (narrative()!.isStale) {
            <div class="narrative-stale">updating...</div>
          }
        </div>
      }

      <p-card header="Monthly report" subheader="Trends, category shifts, and merchant concentration for the selected month.">
        <div class="flex flex-column md:flex-row gap-3 align-items-end">
          <div class="flex flex-column gap-2">
            <label for="reportMonth">Month</label>
            <input id="reportMonth" type="month" pInputText [ngModel]="selectedMonth()" (ngModelChange)="setMonth($event)" />
          </div>
          <div class="grid flex-1">
            <div class="col-12 md:col-3"><div class="metric-label">Total</div><div class="metric-value">{{ report().totalAmount | currency:'EUR' }}</div></div>
            <div class="col-12 md:col-3"><div class="metric-label">Previous month</div><div class="metric-value">{{ report().previousMonthTotal | currency:'EUR' }}</div></div>
            <div class="col-12 md:col-3"><div class="metric-label">Change</div><div class="metric-value">{{ report().percentChange }}%</div></div>
            <div class="col-12 md:col-3"><div class="metric-label">Rolling 3m avg</div><div class="metric-value">{{ report().rolling3MonthAverage | currency:'EUR' }}</div></div>
          </div>
        </div>
      </p-card>

      <div class="grid charts-row">
        <div class="col-12 xl:col-7 fill-col">
          <p-card header="Daily spend" class="fill-card">
            <div echarts [options]="trendOptions()" class="chart"></div>
          </p-card>
        </div>
        <div class="col-12 xl:col-5 fill-col">
          <p-card header="Category comparison vs full previous month" class="fill-card">
            <div class="table-scroll-wrapper">
              <p-table [value]="report().categoryComparisons" responsiveLayout="scroll">
                <ng-template pTemplate="header">
                  <tr><th>Category</th><th>Current</th><th>Previous</th><th>Δ%</th></tr>
                </ng-template>
                <ng-template pTemplate="body" let-item>
                  <tr>
                    <td>{{ item.name }}</td>
                    <td>{{ item.currentAmount | currency:'EUR' }}</td>
                    <td>{{ item.previousAmount | currency:'EUR' }}</td>
                    <td [style.color]="item.deltaPercent > 0 ? 'var(--red-400)' : item.deltaPercent < 0 ? 'var(--green-400)' : ''">
                      {{ item.deltaPercent > 0 ? '+' : '' }}{{ item.deltaPercent }}%
                    </td>
                  </tr>
                </ng-template>
              </p-table>
            </div>
          </p-card>
        </div>
      </div>

      <p-card header="Top merchants">
        <p-table [value]="report().topMerchants" responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr><th>Merchant</th><th>Transactions</th><th>Total</th></tr>
          </ng-template>
          <ng-template pTemplate="body" let-merchant>
            <tr>
              <td>{{ merchant.name }}</td>
              <td>{{ merchant.transactionCount }}</td>
              <td>{{ merchant.totalAmount | currency:'EUR' }}</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `,
  styles: [`
    .narrative-card {
      background: color-mix(in srgb, var(--primary-color) 5%, var(--surface-card));
      border: 1px solid color-mix(in srgb, var(--primary-color) 20%, var(--surface-border));
      border-radius: 12px;
      padding: 1.25rem 1.5rem;
    }

    .narrative-content {
      font-size: 1.05rem;
      line-height: 1.6;
      font-style: italic;
      color: var(--text-color);
    }

    .narrative-stale {
      margin-top: 0.5rem;
      font-size: 0.72rem;
      color: var(--yellow-500);
    }

    .charts-row { align-items: stretch; }

    .fill-col { display: flex; flex-direction: column; }

    :host ::ng-deep .fill-card,
    :host ::ng-deep .fill-card .p-card,
    :host ::ng-deep .fill-card .p-card-body,
    :host ::ng-deep .fill-card .p-card-content {
      flex: 1;
      display: flex;
      flex-direction: column;
    }

    :host ::ng-deep .fill-card .p-card-content { padding-top: 0; }

    .chart { width: 100%; height: 26rem; }

    .table-scroll-wrapper { flex: 1; overflow-y: auto; max-height: 26rem; }

    .metric-label { color: var(--text-color-secondary); font-size: 0.875rem; }
    .metric-value { font-size: 1.35rem; font-weight: 600; margin-top: 0.35rem; }
  `]
})
export class MonthlyReportComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedMonth = signal(`${MONTHLY_FALLBACK.year}-${String(MONTHLY_FALLBACK.month).padStart(2, '0')}`);
  protected readonly report = signal<MonthlyReport>(MONTHLY_FALLBACK);
  protected readonly narrative = signal<NarrativeResponse | null>(null);

  protected readonly trendOptions = computed<EChartsOption>(() => {
    const daily = this.report().dailySpending;
    const categoryData = this.report().dailyCategoryBreakdown;

    // Build lookup: datePrefix (YYYY-MM-DD) -> categoryName -> amount
    const categoryLookup = new Map<string, Map<string, number>>();
    for (const entry of categoryData) {
      const dateKey = entry.date.substring(0, 10);
      if (!categoryLookup.has(dateKey)) categoryLookup.set(dateKey, new Map());
      categoryLookup.get(dateKey)!.set(entry.categoryName, entry.amount);
    }

    // Unique ordered categories (order preserved from backend: highest total first)
    const categories: { name: string; color: string }[] = [];
    const seen = new Set<string>();
    for (const entry of categoryData) {
      if (!seen.has(entry.categoryName)) {
        seen.add(entry.categoryName);
        categories.push({ name: entry.categoryName, color: entry.color ?? '#94a3b8' });
      }
    }

    const xLabels = daily.map(p => new Date(p.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
    const datePrefixes = daily.map(p => p.date.substring(0, 10));

    const barSeries: EChartsOption['series'] = categories.map(cat => ({
      name: cat.name,
      type: 'bar' as const,
      stack: 'total',
      emphasis: { focus: 'series' as const },
      itemStyle: { color: cat.color },
      data: datePrefixes.map(d => categoryLookup.get(d)?.get(cat.name) ?? 0)
    }));

    const rolling7dSeries: EChartsOption['series'] = [{
      name: 'Rolling 7d avg',
      type: 'line' as const,
      smooth: true,
      data: daily.map(p => p.rolling7Day ?? p.amount),
      lineStyle: { width: 2, color: '#f97316' },
      itemStyle: { color: '#f97316' },
      symbol: 'none',
      z: 10
    }];

    return {
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      legend: { top: 0, type: 'scroll', textStyle: { fontSize: 11 } },
      grid: { left: 24, right: 24, top: 56, bottom: 24, containLabel: true },
      xAxis: { type: 'category', data: xLabels },
      yAxis: { type: 'value' },
      series: [...(barSeries as any[]), ...(rolling7dSeries as any[])]
    };
  });

  constructor() {
    this.loadReport(MONTHLY_FALLBACK.year, MONTHLY_FALLBACK.month);
  }

  protected setMonth(value: string): void {
    this.selectedMonth.set(value);
    const [year, month] = value.split('-').map((part) => Number(part));
    if (!year || !month) {
      return;
    }

    this.loadReport(year, month);
  }

  private loadReport(year: number, month: number): void {
    forkJoin({
      report: this.apiService.getMonthlyAnalytics(year, month).pipe(
        catchError(() => of({ ...MONTHLY_FALLBACK, year, month }))
      ),
      narrative: this.apiService.getMonthlyNarrative(year, month).pipe(
        catchError(() => of(null))
      )
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ report, narrative }) => {
      this.report.set(report);
      this.narrative.set(narrative);
    });
  }
}

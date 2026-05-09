import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SelectButtonModule } from 'primeng/selectbutton';
import { FormsModule } from '@angular/forms';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import { ApiService, DashboardAnalytics } from '../../core/services/api.service';

const DASHBOARD_FALLBACK: DashboardAnalytics = {
  kpi: { currentMonth: 0, last30Days: 0, prev30Days: 0, percentChange: 0 },
  categoryBreakdown: [],
  dailySpending: [],
  topMerchants: [],
  recentTransactions: [],
  ytd: { total: 0, avgPerDay: 0, projectedEoy: 0, yoyDelta: null, topCategories: [] },
  income: null
};

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, TableModule, TagModule, SelectButtonModule, NgxEchartsDirective],
  template: `
    <!-- ── TAB TOGGLE ───────────────────────────────────────────── -->
    <div class="flex align-items-center justify-content-between mb-4">
      <p-selectButton [options]="tabs" [(ngModel)]="activeTab" optionLabel="label" optionValue="value" styleClass="dash-tabs"></p-selectButton>
    </div>

    <!-- ═══════════════════════ SPENDING TAB ═══════════════════════ -->
    <ng-container *ngIf="activeTab() === 'spending'">
      <div class="grid">
        <div class="col-12 md:col-6 xl:col-3" *ngFor="let item of kpiCards()">
          <p-card>
            <div class="text-sm text-color-secondary">{{ item.label }}</div>
            <div class="text-3xl font-semibold mt-2">{{ item.value | currency:'EUR':'symbol':'1.2-2' }}</div>
            <small [class]="item.muted ? 'text-color-secondary' : (item.value >= 0 ? 'text-red-400' : 'text-green-400')">{{ item.helper }}</small>
          </p-card>
        </div>

        <div class="col-12 xl:col-5">
          <p-card header="Spending by category" subheader="Last 30 days">
            <div echarts [options]="breakdownOptions()" class="chart"></div>
          </p-card>
        </div>

        <div class="col-12 xl:col-7">
          <p-card header="Spending trend" subheader="Daily spend with rolling averages">
            <div echarts [options]="trendOptions()" class="chart"></div>
          </p-card>
        </div>

        <div class="col-12 xl:col-5">
          <p-card header="Top merchants">
            <p-table [value]="analytics()?.topMerchants ?? []" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr><th>Merchant</th><th>Txns</th><th>Total</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-merchant>
                <tr>
                  <td>{{ merchant.name }}</td>
                  <td>{{ merchant.transactionCount }}</td>
                  <td>{{ merchant.totalAmount | currency:'EUR':'symbol':'1.2-2' }}</td>
                </tr>
              </ng-template>
            </p-table>
          </p-card>
        </div>

        <div class="col-12 xl:col-7">
          <p-card header="Recent transactions">
            <p-table [value]="spendingTransactions()" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr><th>Date</th><th>Merchant</th><th>Category</th><th>Amount</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-t>
                <tr>
                  <td>{{ t.transactionDate | date:'dd.MM.yy' }}</td>
                  <td>{{ t.merchantNormalized || 'Manual entry' }}</td>
                  <td>{{ t.parentCategoryName || t.categoryName }}</td>
                  <td>{{ t.amount | currency:'EUR':'symbol':'1.2-2' }}</td>
                </tr>
              </ng-template>
            </p-table>
          </p-card>
        </div>

        <div class="col-12">
          <p-card header="Year to date">
            <div class="grid">
              <div class="col-6 md:col-3">
                <div class="metric-label">YTD spending</div>
                <div class="metric-value">{{ analytics()?.ytd?.total | currency:'EUR':'symbol':'1.2-2' }}</div>
              </div>
              <div class="col-6 md:col-3">
                <div class="metric-label">Average daily spend</div>
                <div class="metric-value">{{ analytics()?.ytd?.avgPerDay | currency:'EUR':'symbol':'1.2-2' }}</div>
              </div>
              <div class="col-6 md:col-3">
                <div class="metric-label">Projected year-end</div>
                <div class="metric-value">{{ analytics()?.ytd?.projectedEoy | currency:'EUR':'symbol':'1.2-2' }}</div>
              </div>
              <div class="col-6 md:col-3">
                <div class="metric-label">YoY delta</div>
                <div class="metric-value">{{ analytics()?.ytd?.yoyDelta ?? 0 }}%</div>
              </div>
            </div>
            <div class="flex flex-wrap gap-2 mt-4">
              <p-tag *ngFor="let c of analytics()?.ytd?.topCategories ?? []" [value]="c.name + ': ' + (c.amount | currency:'EUR':'symbol':'1.0-0')"></p-tag>
            </div>
          </p-card>
        </div>
      </div>
    </ng-container>

    <!-- ═══════════════════════ INCOME TAB ════════════════════════ -->
    <ng-container *ngIf="activeTab() === 'income'">
      <div class="grid">
        <div class="col-12 md:col-6 xl:col-3" *ngFor="let item of incomeKpiCards()">
          <p-card>
            <div class="text-sm text-color-secondary">{{ item.label }}</div>
            <div class="text-3xl font-semibold mt-2" [class]="item.colorClass">{{ item.value | currency:'EUR':'symbol':'1.2-2' }}</div>
            <small class="text-color-secondary">{{ item.helper }}</small>
          </p-card>
        </div>

        <div class="col-12 xl:col-5">
          <p-card header="Income sources" subheader="Where your money comes from">
            <div echarts [options]="incomeSourcesOptions()" class="chart"></div>
          </p-card>
        </div>

        <div class="col-12 xl:col-7">
          <p-card header="Income vs Spending" subheader="Last 6 months comparison">
            <div echarts [options]="incomeVsSpendingOptions()" class="chart"></div>
          </p-card>
        </div>

        <div class="col-12 xl:col-5">
          <p-card header="Income by source">
            <p-table [value]="analytics()?.income?.sources ?? []" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr><th>Source</th><th>Txns</th><th>Total</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-source>
                <tr>
                  <td class="font-medium">{{ source.name }}</td>
                  <td>{{ source.transactionCount }}</td>
                  <td class="text-green-400 font-medium">+{{ source.totalAmount | currency:'EUR':'symbol':'1.2-2' }}</td>
                </tr>
              </ng-template>
              <ng-template pTemplate="emptymessage">
                <tr><td colspan="3" class="text-center text-color-secondary">No income recorded yet.</td></tr>
              </ng-template>
            </p-table>
          </p-card>
        </div>

        <div class="col-12 xl:col-7">
          <p-card header="Monthly net cash flow">
            <div echarts [options]="netCashFlowOptions()" class="chart"></div>
          </p-card>
        </div>

        <div class="col-12">
          <p-card header="Recent income">
            <p-table [value]="incomeTransactions()" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr><th>Date</th><th>From</th><th>Notes</th><th>Amount</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-t>
                <tr>
                  <td>{{ t.transactionDate | date:'dd.MM.yy' }}</td>
                  <td class="font-medium">{{ t.merchantNormalized || t.merchantRaw || '—' }}</td>
                  <td class="text-color-secondary text-sm">{{ t.notes || '—' }}</td>
                  <td class="text-green-400 font-medium">+{{ t.amount | currency:'EUR':'symbol':'1.2-2' }}</td>
                </tr>
              </ng-template>
              <ng-template pTemplate="emptymessage">
                <tr><td colspan="4" class="text-center text-color-secondary">No income transactions yet.</td></tr>
              </ng-template>
            </p-table>
          </p-card>
        </div>
      </div>
    </ng-container>
  `,
  styles: [`
    .chart { width: 100%; height: 22rem; }
    .metric-label { color: var(--text-color-secondary); font-size: 0.875rem; }
    .metric-value { font-size: 1.5rem; font-weight: 600; margin-top: 0.35rem; }
    :host ::ng-deep .dash-tabs .p-button { min-width: 7rem; }
  `]
})
export class DashboardComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly analytics = signal<DashboardAnalytics | null>(null);
  protected readonly loading = signal(true);
  protected readonly activeTab = signal<'spending' | 'income'>('spending');

  protected readonly tabs = [
    { label: '💸 Spending', value: 'spending' },
    { label: '💰 Income', value: 'income' }
  ];

  protected readonly spendingTransactions = computed(() =>
    (this.analytics()?.recentTransactions ?? []).filter((t) => t.direction === 'Debit')
  );

  protected readonly incomeTransactions = computed(() =>
    (this.analytics()?.recentTransactions ?? []).filter((t) => t.direction === 'Credit')
  );

  /* ── Spending KPIs ─────────────────────────────── */
  protected readonly kpiCards = computed(() => {
    const kpi = this.analytics()?.kpi ?? DASHBOARD_FALLBACK.kpi;
    return [
      { label: 'Current month', value: kpi.currentMonth, helper: 'Month-to-date spend', muted: true },
      { label: 'Last 30 days', value: kpi.last30Days, helper: 'Rolling window total', muted: true },
      { label: 'Previous 30 days', value: kpi.prev30Days, helper: 'Reference period', muted: true },
      { label: 'Change', value: kpi.percentChange, helper: `${kpi.percentChange.toFixed(1)}% vs prior window`, muted: false }
    ];
  });

  /* ── Income KPIs ───────────────────────────────── */
  protected readonly incomeKpiCards = computed(() => {
    const inc = this.analytics()?.income;
    if (!inc) return [];
    return [
      { label: 'Income this month', value: inc.currentMonth, helper: 'Month-to-date credits', colorClass: 'text-green-400' },
      { label: 'Income last 30d', value: inc.last30Days, helper: 'Rolling 30 day income', colorClass: 'text-green-400' },
      { label: 'YTD income', value: inc.ytdTotal, helper: 'Total received this year', colorClass: 'text-green-400' },
      { label: 'Net (30d)', value: inc.netLast30Days, helper: 'Income minus spending', colorClass: inc.netLast30Days >= 0 ? 'text-green-400' : 'text-red-400' }
    ];
  });

  /* ── Spending pie chart ────────────────────────── */
  protected readonly breakdownOptions = computed<EChartsOption>(() => {
    const breakdown = this.analytics()?.categoryBreakdown ?? DASHBOARD_FALLBACK.categoryBreakdown;
    return {
      tooltip: { trigger: 'item', formatter: '{b}: €{c} ({d}%)' },
      legend: { orient: 'vertical', right: '2%', top: 'middle', itemGap: 10, textStyle: { fontSize: 12 } },
      series: [{
        type: 'pie', radius: ['38%', '65%'], center: ['38%', '50%'],
        avoidLabelOverlap: true, label: { show: false },
        emphasis: { label: { show: true, fontSize: 13, fontWeight: 'bold' } },
        data: breakdown.map((item) => ({ name: item.name, value: item.amount, itemStyle: { color: item.color ?? undefined } }))
      }]
    };
  });

  /* ── Spending trend line ───────────────────────── */
  protected readonly trendOptions = computed<EChartsOption>(() => {
    const series = this.analytics()?.dailySpending ?? DASHBOARD_FALLBACK.dailySpending;
    const labels = series.map((item) => new Date(item.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
    return {
      tooltip: { trigger: 'axis' },
      legend: { top: 0 },
      grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
      xAxis: { type: 'category', data: labels },
      yAxis: { type: 'value' },
      series: [
        { name: 'Daily spend', type: 'line', smooth: true, data: series.map((i) => i.amount) },
        { name: 'Rolling 7d', type: 'line', smooth: true, data: series.map((i) => i.rolling7Day ?? i.amount) },
        { name: 'Rolling 30d', type: 'line', smooth: true, data: series.map((i) => i.rolling30Day ?? i.amount) }
      ]
    };
  });

  /* ── Income sources pie ────────────────────────── */
  protected readonly incomeSourcesOptions = computed<EChartsOption>(() => {
    const sources = this.analytics()?.income?.sources ?? [];
    const greens = ['#10b981', '#34d399', '#6ee7b7', '#a7f3d0', '#059669', '#047857', '#065f46', '#14b8a6', '#2dd4bf', '#5eead4'];
    return {
      tooltip: { trigger: 'item', formatter: '{b}: €{c} ({d}%)' },
      legend: { orient: 'horizontal', bottom: 0, type: 'scroll', itemGap: 12, textStyle: { fontSize: 12 } },
      series: [{
        type: 'pie', radius: ['35%', '62%'], center: ['50%', '45%'],
        avoidLabelOverlap: true, label: { show: false },
        emphasis: { label: { show: true, fontSize: 13, fontWeight: 'bold' } },
        data: sources.map((s, i) => ({ name: s.name, value: s.totalAmount, itemStyle: { color: greens[i % greens.length] } }))
      }]
    };
  });

  /* ── Income vs Spending bar chart ──────────────── */
  protected readonly incomeVsSpendingOptions = computed<EChartsOption>(() => {
    const months = this.analytics()?.income?.monthlyComparison ?? [];
    return {
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      legend: { top: 0, data: ['Income', 'Spending'] },
      grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
      xAxis: { type: 'category', data: months.map((m) => m.label) },
      yAxis: { type: 'value' },
      series: [
        { name: 'Income', type: 'bar', data: months.map((m) => m.income), itemStyle: { color: '#10b981' }, barMaxWidth: 36 },
        { name: 'Spending', type: 'bar', data: months.map((m) => m.spending), itemStyle: { color: '#ef4444' }, barMaxWidth: 36 }
      ]
    };
  });

  /* ── Net cash flow waterfall ───────────────────── */
  protected readonly netCashFlowOptions = computed<EChartsOption>(() => {
    const months = this.analytics()?.income?.monthlyComparison ?? [];
    return {
      tooltip: { trigger: 'axis', formatter: (params: any) => {
        const p = Array.isArray(params) ? params[0] : params;
        const val = p.value as number;
        return `${p.name}<br/>Net: <strong style="color:${val >= 0 ? '#10b981' : '#ef4444'}">€${val.toFixed(2)}</strong>`;
      }},
      grid: { left: 24, right: 24, top: 24, bottom: 24, containLabel: true },
      xAxis: { type: 'category', data: months.map((m) => m.label) },
      yAxis: { type: 'value' },
      series: [{
        type: 'bar', barMaxWidth: 48,
        data: months.map((m) => ({
          value: m.net,
          itemStyle: { color: m.net >= 0 ? '#10b981' : '#ef4444', borderRadius: m.net >= 0 ? [4, 4, 0, 0] : [0, 0, 4, 4] }
        }))
      }]
    };
  });

  constructor() {
    this.apiService.getDashboardAnalytics().pipe(
      catchError(() => of(DASHBOARD_FALLBACK)),
      finalize(() => this.loading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((analytics) => this.analytics.set(analytics));
  }
}



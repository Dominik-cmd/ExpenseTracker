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

import { ApiService, Category, NarrativeResponse, YearlyReport } from '../../../core/services/api.service';

const currentYear = new Date().getFullYear();
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const FALLBACK_CATEGORY_COLOR = '#94a3b8';
const YEARLY_FALLBACK: YearlyReport = {
  year: currentYear,
  yearTotal: 0,
  previousYearTotal: 0,
  percentChange: 0,
  monthlyCategories: [],
  largestTransactions: [],
  categoryEvolution: []
};

@Component({
  standalone: true,
  selector: 'app-yearly-report',
  imports: [CommonModule, FormsModule, CardModule, InputTextModule, TableModule, NgxEchartsDirective],
  template: `
    <div class="flex flex-column gap-4">
      @if (narrative()?.content) {
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
      }

      <p-card header="Yearly report" subheader="YTD totals, category evolution, and largest transactions for the selected year.">
        <div class="flex flex-column md:flex-row gap-3 align-items-end">
          <div class="flex flex-column gap-2">
            <label for="reportYear">Year</label>
            <input id="reportYear" type="number" min="2020" max="2100" pInputText [ngModel]="selectedYear()" (ngModelChange)="setYear($event)" />
          </div>
          <div class="grid flex-1">
            <div class="col-12 md:col-4"><div class="metric-label">Year total</div><div class="metric-value">{{ report().yearTotal | currency:'EUR' }}</div></div>
            <div class="col-12 md:col-4"><div class="metric-label">Previous year</div><div class="metric-value">{{ report().previousYearTotal | currency:'EUR' }}</div></div>
            <div class="col-12 md:col-4"><div class="metric-label">Change</div><div class="metric-value">{{ report().percentChange }}%</div></div>
          </div>
        </div>
      </p-card>

      <p-card header="Category evolution">
        <div class="flex flex-wrap gap-2 mb-3">
          @for (cat of report().categoryEvolution; track cat.categoryName) {
            <span
              class="category-chip"
              [class.category-chip--active]="selectedCategories().includes(cat.categoryName)"
              [style.--category-chip-color]="getCategoryColor(cat.categoryName, cat.color)"
              (click)="toggleCategory(cat.categoryName)">
              {{ cat.categoryName }}
            </span>
          }
        </div>
        <div echarts [options]="evolutionOptions()" class="chart"></div>
      </p-card>

      <p-card header="Month × category" subheader="Top 8 categories by annual spend · colour intensity = relative share within each column">
        @if (heatmapData().rows.length) {
          <div class="heatmap-scroll">
            <table class="heatmap-table">
              <thead>
                <tr>
                  <th class="heatmap-month-col"></th>
                  @for (col of heatmapData().colTotals; track col.name) {
                    <th class="heatmap-cat-header" [title]="col.name">{{ col.name }}</th>
                  }
                  <th class="heatmap-total-header">Total</th>
                </tr>
              </thead>
              <tbody>
                @for (row of heatmapData().rows; track row.month) {
                  <tr>
                    <td class="heatmap-month-label">{{ row.label }}</td>
                    @for (cell of row.cells; track $index) {
                      <td class="heatmap-cell" [style.background]="cellBackground(cell.intensity, heatmapData().colTotals[$index].color)">
                        @if (cell.amount > 0) { {{ cell.amount | currency:'EUR':'symbol':'1.0-0' }} }
                      </td>
                    }
                    <td class="heatmap-row-total">{{ row.total | currency:'EUR':'symbol':'1.0-0' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr class="heatmap-foot">
                  <td class="heatmap-month-label">Total</td>
                  @for (col of heatmapData().colTotals; track col.name) {
                    <td class="heatmap-cell heatmap-foot-cell">{{ col.total | currency:'EUR':'symbol':'1.0-0' }}</td>
                  }
                  <td class="heatmap-row-total heatmap-foot-cell">{{ grandTotal() | currency:'EUR':'symbol':'1.0-0' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        } @else {
          <div class="empty-state">No monthly category data available for {{ selectedYear() }}.</div>
        }
      </p-card>

      <p-card header="Largest transactions">
        <p-table [value]="report().largestTransactions" responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr><th>Date</th><th>Merchant</th><th>Category</th><th>Amount</th></tr>
          </ng-template>
          <ng-template pTemplate="body" let-transaction>
            <tr>
              <td>{{ transaction.transactionDate | date:'mediumDate' }}</td>
              <td>{{ transaction.merchantNormalized || 'Manual entry' }}</td>
              <td>{{ transaction.parentCategoryName || transaction.categoryName }}</td>
              <td>{{ transaction.amount | currency:'EUR' }}</td>
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

    .chart { width: 100%; height: 24rem; }
    .metric-label { color: var(--text-color-secondary); font-size: 0.875rem; }
    .metric-value { font-size: 1.35rem; font-weight: 600; margin-top: 0.35rem; }

    .heatmap-scroll {
      overflow-x: auto;
    }

    .heatmap-table {
      width: 100%;
      border-collapse: separate;
      border-spacing: 3px;
      min-width: 560px;
    }

    .heatmap-cat-header {
      font-size: 0.72rem;
      font-weight: 600;
      color: var(--text-color-secondary);
      text-align: right;
      padding: 0 0.4rem 0.6rem;
      max-width: 80px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .heatmap-total-header {
      font-size: 0.72rem;
      font-weight: 600;
      color: var(--text-color-secondary);
      text-align: right;
      padding: 0 0 0.6rem 0.75rem;
      white-space: nowrap;
    }

    .heatmap-month-col { min-width: 36px; }

    .heatmap-month-label {
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--text-color-secondary);
      padding: 0.35rem 0.75rem 0.35rem 0;
      white-space: nowrap;
    }

    .heatmap-cell {
      text-align: right;
      font-size: 0.78rem;
      color: var(--text-color);
      padding: 0.35rem 0.5rem;
      border-radius: 5px;
      min-width: 68px;
      transition: background 0.15s;
    }

    .heatmap-row-total {
      text-align: right;
      font-size: 0.8rem;
      font-weight: 600;
      padding-left: 0.75rem;
      color: var(--text-color);
      white-space: nowrap;
    }

    .heatmap-foot .heatmap-month-label {
      font-weight: 700;
      color: var(--text-color);
    }

    .heatmap-foot-cell {
      font-weight: 600;
      border-top: 1px solid var(--surface-border);
      padding-top: 0.5rem;
    }

    .empty-state {
      padding: 1.5rem;
      text-align: center;
      color: var(--text-color-secondary);
    }

    .category-chip {
      padding: 0.35rem 0.75rem;
      border-radius: 1rem;
      font-size: 0.8rem;
      font-weight: 500;
      cursor: pointer;
      border: 1px solid var(--category-chip-color, var(--surface-border));
      background: var(--surface-ground);
      color: var(--category-chip-color, var(--text-color-secondary));
      transition: all 0.15s;
      user-select: none;
    }

    .category-chip--active {
      background: var(--category-chip-color, var(--primary-color));
      color: #fff;
      border-color: var(--category-chip-color, var(--primary-color));
    }

    .category-chip:hover {
      border-color: var(--category-chip-color, var(--primary-color));
    }
  `]
})
export class YearlyReportComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedYear = signal(currentYear);
  protected readonly report = signal<YearlyReport>(YEARLY_FALLBACK);
  protected readonly narrative = signal<NarrativeResponse | null>(null);
  protected readonly categories = signal<Category[]>([]);
  protected readonly selectedCategories = signal<string[]>([]);
  protected readonly categoryColors = computed(() => {
    const colors = new Map<string, string>();

    const visit = (categories: Category[]): void => {
      for (const category of categories) {
        if (category.color) {
          colors.set(category.name, category.color);
        }

        if (category.subCategories.length) {
          visit(category.subCategories);
        }
      }
    };

    visit(this.categories());
    return colors;
  });
  protected readonly visibleCategoryEvolution = computed(() => {
    const selected = new Set(this.selectedCategories());
    return this.report().categoryEvolution.filter((series) => selected.has(series.categoryName));
  });

  protected readonly heatmapData = computed(() => {
    const items = this.report().monthlyCategories;

    const categoryTotals = new Map<string, number>();
    for (const item of items) {
      categoryTotals.set(item.categoryName, (categoryTotals.get(item.categoryName) ?? 0) + item.amount);
    }

    const topCategories = [...categoryTotals.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 8)
      .map(([name]) => name);

    const lookup = new Map<string, number>();
    for (const item of items) {
      lookup.set(`${item.month}:${item.categoryName}`, item.amount);
    }

    const colMax = topCategories.map((cat) => {
      let max = 0;
      for (let m = 1; m <= 12; m++) {
        const v = lookup.get(`${m}:${cat}`) ?? 0;
        if (v > max) max = v;
      }
      return max;
    });

    const activeMonths = [...new Set(items.map((i) => i.month))].sort((a, b) => a - b);

    const rows = activeMonths.map((month) => ({
      month,
      label: MONTH_LABELS[month - 1],
      cells: topCategories.map((cat, ci) => {
        const amount = lookup.get(`${month}:${cat}`) ?? 0;
        return { amount, intensity: colMax[ci] > 0 ? amount / colMax[ci] : 0 };
      }),
      total: topCategories.reduce((sum, cat) => sum + (lookup.get(`${month}:${cat}`) ?? 0), 0)
    }));

    const colTotals = topCategories.map((cat) => ({
      name: cat,
      total: activeMonths.reduce((sum, month) => sum + (lookup.get(`${month}:${cat}`) ?? 0), 0),
      color: this.getCategoryColor(cat, null)
    }));

    return { topCategories, colTotals, rows };
  });

  protected readonly grandTotal = computed(() =>
    this.heatmapData().colTotals.reduce((sum, col) => sum + col.total, 0)
  );

  protected readonly evolutionOptions = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'axis' },
    legend: { top: 0, data: this.visibleCategoryEvolution().map((series) => series.categoryName) },
    grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
    xAxis: { type: 'category', data: MONTH_LABELS },
    yAxis: { type: 'value' },
    series: this.visibleCategoryEvolution().map((series) => {
      const color = this.getCategoryColor(series.categoryName, series.color);
      return {
        name: series.categoryName,
        type: 'line',
        stack: 'total',
        areaStyle: { color, opacity: 0.18 },
        smooth: true,
        lineStyle: { color },
        itemStyle: { color },
        data: series.values.map((value) => value.amount)
      };
    })
  }));

  constructor() {
    this.loadCategories();
    this.loadReport(currentYear);
  }

  protected setYear(value: number | string): void {
    const year = Number(value);
    if (!Number.isFinite(year)) {
      return;
    }

    this.selectedYear.set(year);
    this.loadReport(year);
  }

  protected toggleCategory(name: string): void {
    this.selectedCategories.update((current) => {
      if (current.includes(name)) {
        return current.filter((categoryName) => categoryName !== name);
      }

      const next = [...current, name];
      return next.length > 4 ? next.slice(1) : next;
    });
  }

  protected getCategoryColor(name: string, fallback?: string | null): string {
    return this.categoryColors().get(name) ?? fallback ?? FALLBACK_CATEGORY_COLOR;
  }

  protected cellBackground(intensity: number, color: string): string {
    if (intensity === 0 || !color || color.length < 6) return 'transparent';
    const hex = color.replace('#', '');
    if (hex.length !== 6) return 'transparent';
    const r = parseInt(hex.substring(0, 2), 16);
    const g = parseInt(hex.substring(2, 4), 16);
    const b = parseInt(hex.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${Math.max(0.07, intensity * 0.55)})`;
  }

  private loadCategories(): void {
    this.apiService.getCategories().pipe(
      catchError(() => of([] as Category[])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((categories) => this.categories.set(categories));
  }

  private loadReport(year: number): void {
    forkJoin({
      report: this.apiService.getYearlyAnalytics(year).pipe(
        catchError(() => of({ ...YEARLY_FALLBACK, year }))
      ),
      narrative: this.apiService.getYearlyNarrative(year).pipe(
        catchError(() => of(null))
      )
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ report, narrative }) => {
      this.report.set(report);
      this.narrative.set(narrative);

      const top3 = [...report.categoryEvolution]
        .sort((a, b) => b.values.reduce((sum, value) => sum + value.amount, 0) - a.values.reduce((sum, value) => sum + value.amount, 0))
        .slice(0, 3)
        .map((category) => category.categoryName);

      this.selectedCategories.set(top3);
    });
  }
}



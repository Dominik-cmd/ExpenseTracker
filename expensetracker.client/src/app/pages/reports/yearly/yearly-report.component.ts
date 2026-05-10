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

      <div class="grid">
        <div class="col-12 xl:col-6">
          <p-card header="Month × category grid">
            <p-table [value]="report().monthlyCategories" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr><th>Month</th><th>Category</th><th>Amount</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-item>
                <tr>
                  <td>{{ item.month }}</td>
                  <td>{{ item.categoryName }}</td>
                  <td>{{ item.amount | currency:'EUR' }}</td>
                </tr>
              </ng-template>
            </p-table>
          </p-card>
        </div>
        <div class="col-12 xl:col-6">
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
      </div>
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



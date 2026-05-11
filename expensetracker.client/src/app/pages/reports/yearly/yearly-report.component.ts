import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';

import { EMPTY_YEARLY_REPORT } from '../../../core/constants/fallbacks';
import { Category, NarrativeResponse, YearlyReport } from '../../../core/models';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { CategoryService } from '../../../core/services/category.service';
import { CategoryEvolutionChartComponent } from './category-evolution-chart.component';
import { MonthCategoryGridComponent } from './month-category-grid.component';
import { AppCurrencyPipe } from '../../../shared';

const currentYear = new Date().getFullYear();
const FALLBACK_CATEGORY_COLOR = '#94a3b8';

@Component({
  standalone: true,
  selector: 'app-yearly-report',
  imports: [
    CommonModule,
    FormsModule,
    CardModule,
    InputTextModule,
    TableModule,
    CategoryEvolutionChartComponent,
    MonthCategoryGridComponent,
    AppCurrencyPipe
  ],
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

      <p-card header="Yearly report" subheader="YTD totals, category evolution, daily intensity, and largest transactions for the selected year.">
        <div class="flex flex-column md:flex-row gap-3 align-items-end">
          <div class="flex flex-column gap-2">
            <label for="reportYear">Year</label>
            <input id="reportYear" type="number" min="2020" max="2100" pInputText [ngModel]="selectedYear()" (ngModelChange)="setYear($event)" />
          </div>
          <div class="grid flex-1">
            <div class="col-12 md:col-4">
              <div class="metric-label">Year total</div>
              <div class="metric-value">{{ report().yearTotal | appCurrency }}</div>
            </div>
            <div class="col-12 md:col-4">
              <div class="metric-label">Previous year</div>
              <div class="metric-value">{{ report().previousYearTotal | appCurrency }}</div>
            </div>
            <div class="col-12 md:col-4">
              <div class="metric-label">Change</div>
              <div class="metric-value">{{ report().percentChange }}%</div>
            </div>
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
        <app-category-evolution-chart
          [series]="visibleCategoryEvolution()"
          [categoryColorMap]="categoryColors()"></app-category-evolution-chart>
      </p-card>

      <p-card header="Month × category">
        <app-month-category-grid
          [monthlyCategories]="report().monthlyCategories"
          [categoryEvolution]="report().categoryEvolution"></app-month-category-grid>
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
              <td>{{ transaction.amount | appCurrency }}</td>
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

    .metric-label {
      color: var(--text-color-secondary);
      font-size: 0.875rem;
    }

    .metric-value {
      margin-top: 0.35rem;
      font-size: 1.35rem;
      font-weight: 600;
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
  private readonly analyticsService = inject(AnalyticsService);
  private readonly categoryService = inject(CategoryService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedYear = signal(currentYear);
  protected readonly report = signal<YearlyReport>(EMPTY_YEARLY_REPORT);
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
    this.categoryService.getCategories().pipe(
      catchError(() => of([] as Category[])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((categories) => this.categories.set(categories));
  }

  private loadReport(year: number): void {
    forkJoin({
      report: this.analyticsService.getYearlyAnalytics(year).pipe(
        catchError(() => of({ ...EMPTY_YEARLY_REPORT, year }))
      ),
      narrative: this.analyticsService.getYearlyNarrative(year).pipe(
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

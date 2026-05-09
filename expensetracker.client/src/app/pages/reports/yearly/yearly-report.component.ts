import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import { ApiService, YearlyReport } from '../../../core/services/api.service';

const currentYear = new Date().getFullYear();
const YEARLY_FALLBACK: YearlyReport = {
  year: currentYear,
  yearTotal: 8450.2,
  previousYearTotal: 8032.7,
  percentChange: 5.2,
  monthlyCategories: [
    { month: 1, categoryName: 'Groceries', amount: 280 },
    { month: 2, categoryName: 'Groceries', amount: 310 },
    { month: 1, categoryName: 'Fuel', amount: 120 },
    { month: 2, categoryName: 'Fuel', amount: 140 }
  ],
  largestTransactions: [
    {
      id: '1', userId: 'demo', amount: 640, currency: 'EUR', direction: 'Debit', transactionType: 'Transfer', transactionDate: new Date().toISOString(),
      merchantRaw: 'Landlord', merchantNormalized: 'LANDLORD', categoryId: 'housing', categorySource: 'Manual', transactionSource: 'Manual', notes: 'Rent', isDeleted: false,
      rawMessageId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), categoryName: 'Housing', parentCategoryName: null
    }
  ],
  categoryEvolution: [
    { categoryName: 'Groceries', values: Array.from({ length: 12 }, (_, index) => ({ month: index + 1, amount: 250 + index * 12 })) },
    { categoryName: 'Fuel', values: Array.from({ length: 12 }, (_, index) => ({ month: index + 1, amount: 90 + index * 6 })) },
    { categoryName: 'Dining', values: Array.from({ length: 12 }, (_, index) => ({ month: index + 1, amount: 75 + index * 4 })) }
  ]
};

@Component({
  standalone: true,
  selector: 'app-yearly-report',
  imports: [CommonModule, FormsModule, CardModule, InputTextModule, TableModule, NgxEchartsDirective],
  template: `
    <div class="flex flex-column gap-4">
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
    .chart { width: 100%; height: 24rem; }
    .metric-label { color: var(--text-color-secondary); font-size: 0.875rem; }
    .metric-value { font-size: 1.35rem; font-weight: 600; margin-top: 0.35rem; }
  `]
})
export class YearlyReportComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedYear = signal(currentYear);
  protected readonly report = signal<YearlyReport>(YEARLY_FALLBACK);

  protected readonly evolutionOptions = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'axis' },
    legend: { top: 0 },
    grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
    xAxis: { type: 'category', data: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'] },
    yAxis: { type: 'value' },
    series: this.report().categoryEvolution.map((series) => ({
      name: series.categoryName,
      type: 'line',
      stack: 'total',
      areaStyle: {},
      smooth: true,
      data: series.values.map((value) => value.amount)
    }))
  }));

  constructor() {
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

  private loadReport(year: number): void {
    this.apiService.getYearlyAnalytics(year).pipe(
      catchError(() => of({ ...YEARLY_FALLBACK, year })),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((report) => this.report.set(report));
  }
}



import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import { ApiService, InsightsReport } from '../../../core/services/api.service';

const startOfYear = new Date(new Date().getFullYear(), 0, 1);
const INSIGHTS_FALLBACK: InsightsReport = {
  calendarHeatmap: Array.from({ length: 12 }, (_, index) => ({
    date: new Date(startOfYear.getTime() + index * 86400000 * 7).toISOString(),
    amount: [12, 48, 35, 92, 24, 61, 40, 77, 18, 55, 73, 29][index]
  })),
  dayOfWeekAverages: [
    { dayOfWeek: 'Monday', averageAmount: 28.4, transactionCount: 18 },
    { dayOfWeek: 'Tuesday', averageAmount: 32.1, transactionCount: 14 },
    { dayOfWeek: 'Wednesday', averageAmount: 44.2, transactionCount: 16 },
    { dayOfWeek: 'Thursday', averageAmount: 39.9, transactionCount: 15 },
    { dayOfWeek: 'Friday', averageAmount: 57.3, transactionCount: 21 },
    { dayOfWeek: 'Saturday', averageAmount: 63.1, transactionCount: 11 },
    { dayOfWeek: 'Sunday', averageAmount: 22.5, transactionCount: 9 }
  ],
  recurringTransactions: [
    { merchant: 'NETFLIX', averageAmount: 12.99, latestAmount: 12.99, latestDate: new Date().toISOString(), isAnomaly: false },
    { merchant: 'OMV', averageAmount: 48.2, latestAmount: 61.4, latestDate: new Date(Date.now() - 86400000 * 2).toISOString(), isAnomaly: true }
  ],
  firstTimeMerchants: [
    { merchant: 'LOCAL BAKERY', amount: 8.4, transactionDate: new Date().toISOString() },
    { merchant: 'BOOKSTORE', amount: 23.9, transactionDate: new Date(Date.now() - 86400000 * 4).toISOString() }
  ],
  quietDays: [new Date(Date.now() - 86400000 * 1).toISOString(), new Date(Date.now() - 86400000 * 5).toISOString()]
};

@Component({
  standalone: true,
  selector: 'app-insights',
  imports: [CommonModule, CardModule, TagModule, TableModule, NgxEchartsDirective],
  template: `
    <div class="grid">
      <div class="col-12 xl:col-7">
        <p-card header="Calendar heatmap" subheader="Daily spend intensity across the current year.">
          <div echarts [options]="heatmapOptions()" class="chart"></div>
        </p-card>
      </div>
      <div class="col-12 xl:col-5">
        <p-card header="Day-of-week averages">
          <div echarts [options]="weekdayOptions()" class="chart"></div>
        </p-card>
      </div>

      <div class="col-12 xl:col-6">
        <p-card header="Recurring transactions">
          <p-table [value]="insights().recurringTransactions" responsiveLayout="scroll">
            <ng-template pTemplate="header">
              <tr><th>Merchant</th><th>Average</th><th>Latest</th><th>Status</th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-item>
              <tr>
                <td>{{ item.merchant }}</td>
                <td>{{ item.averageAmount | currency:'EUR' }}</td>
                <td>{{ item.latestAmount | currency:'EUR' }}</td>
                <td><p-tag [value]="item.isAnomaly ? 'Anomaly' : 'Stable'" [severity]="item.isAnomaly ? 'danger' : 'success'"></p-tag></td>
              </tr>
            </ng-template>
          </p-table>
        </p-card>
      </div>

      <div class="col-12 xl:col-6">
        <p-card header="First-time merchants this month">
          <p-table [value]="insights().firstTimeMerchants" responsiveLayout="scroll">
            <ng-template pTemplate="header">
              <tr><th>Merchant</th><th>Date</th><th>Amount</th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-item>
              <tr>
                <td>{{ item.merchant }}</td>
                <td>{{ item.transactionDate | date:'mediumDate' }}</td>
                <td>{{ item.amount | currency:'EUR' }}</td>
              </tr>
            </ng-template>
          </p-table>

          <div class="mt-4 flex flex-wrap gap-2">
            <p-tag *ngFor="let day of insights().quietDays" [value]="('Quiet: ' + (day | date:'MMM d'))"></p-tag>
          </div>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .chart { width: 100%; height: 24rem; }
  `]
})
export class InsightsComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly insights = signal<InsightsReport>(INSIGHTS_FALLBACK);

  protected readonly heatmapOptions = computed<EChartsOption>(() => {
    const values = this.insights().calendarHeatmap.map((point) => point.amount);
    const max = Math.max(...values, 1);

    return {
      tooltip: { position: 'top' },
      visualMap: {
        min: 0,
        max,
        calculable: true,
        orient: 'horizontal',
        left: 'center',
        top: 0
      },
      calendar: {
        top: 60,
        left: 24,
        right: 24,
        cellSize: ['auto', 18],
        range: new Date().getFullYear().toString()
      },
      series: [
        {
          type: 'heatmap',
          coordinateSystem: 'calendar',
          data: this.insights().calendarHeatmap.map((point) => [point.date.slice(0, 10), point.amount])
        }
      ]
    };
  });

  protected readonly weekdayOptions = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'axis' },
    grid: { left: 24, right: 24, top: 24, bottom: 24, containLabel: true },
    xAxis: { type: 'category', data: this.insights().dayOfWeekAverages.map((item) => item.dayOfWeek.slice(0, 3)) },
    yAxis: { type: 'value' },
    series: [
      {
        type: 'bar',
        data: this.insights().dayOfWeekAverages.map((item) => item.averageAmount)
      }
    ]
  }));

  constructor() {
    this.apiService.getInsightsAnalytics().pipe(
      catchError(() => of(INSIGHTS_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((insights) => this.insights.set(insights));
  }
}



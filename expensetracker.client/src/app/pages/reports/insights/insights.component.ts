import { DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import { EMPTY_INSIGHTS_REPORT } from '../../../core/constants/fallbacks';
import { InsightsReport } from '../../../core/models';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { AppCurrencyPipe } from '../../../shared';

@Component({
  standalone: true,
  selector: 'app-insights',
  imports: [AppCurrencyPipe, DatePipe, CardModule, TagModule, TableModule, NgxEchartsDirective],
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
          @if (insights().recurringTransactions.length) {
            <p-table [value]="insights().recurringTransactions" responsiveLayout="scroll">
              <ng-template pTemplate="header">
                <tr><th>Merchant</th><th>Average</th><th>Latest</th><th>Status</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-item>
                <tr>
                  <td>{{ item.merchant }}</td>
                  <td>{{ item.averageAmount | appCurrency }}</td>
                  <td>{{ item.latestAmount | appCurrency }}</td>
                  <td><p-tag [value]="item.isAnomaly ? 'Anomaly' : 'Stable'" [severity]="item.isAnomaly ? 'danger' : 'success'"></p-tag></td>
                </tr>
              </ng-template>
            </p-table>
          } @else {
            <div class="empty-state text-center text-color-secondary">
              <i class="pi pi-info-circle"></i>
              <span>Detection improves with 3+ months of history.</span>
            </div>
          }
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
                <td>{{ item.amount | appCurrency }}</td>
              </tr>
            </ng-template>
          </p-table>

          <div class="mt-4 flex flex-wrap gap-2">
            @for (day of insights().quietDays; track day) {
              <p-tag [value]="('Quiet: ' + (day | date:'MMM d'))"></p-tag>
            }
          </div>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .chart { width: 100%; height: 24rem; }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 2rem 0.75rem;
      color: var(--text-color-secondary);
    }

    .empty-state i {
      font-size: 1rem;
      opacity: 0.8;
    }
  `]
})
export class InsightsComponent {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly insights = signal<InsightsReport>(EMPTY_INSIGHTS_REPORT);

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
    this.analyticsService.getInsightsAnalytics().pipe(
      catchError(() => of(EMPTY_INSIGHTS_REPORT)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((insights) => this.insights.set(insights));
  }
}



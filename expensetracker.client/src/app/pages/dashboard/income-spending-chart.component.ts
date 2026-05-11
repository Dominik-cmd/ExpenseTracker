import { CommonModule } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { CardModule } from 'primeng/card';
import { EChartsOption } from 'echarts';
import { NgxEchartsDirective } from 'ngx-echarts';

import { IncomeWidget } from '../../core/models';
import { CurrencyService } from '../../core/services/currency.service';

@Component({
  standalone: true,
  selector: 'app-income-spending-chart',
  imports: [CommonModule, CardModule, NgxEchartsDirective],
  template: `
    @if (income()) {
      <p-card>
        <div class="widget-header">
          <span class="widget-title">Income vs Spending</span>
          <span class="widget-subtitle">Last 6 months</span>
        </div>

        @if (months().length > 0) {
          <div echarts [options]="chartOptions()" class="income-chart"></div>
        } @else {
          <div class="empty-state">No income data available yet.</div>
        }
      </p-card>
    }
  `,
  styles: [`
    :host {
      display: block;
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

    .income-chart {
      width: 100%;
      height: 20rem;
    }

    .empty-state {
      padding: 1.5rem 0;
      text-align: center;
      color: var(--text-color-secondary);
      font-size: 0.875rem;
    }
  `]
})
export class IncomeSpendingChartComponent {
  readonly income = input<IncomeWidget | null>(null);

  private readonly currencyService = inject(CurrencyService);

  private readonly currencyFormatter = computed(() => new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: this.currencyService.currency(),
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  }));

  protected readonly months = computed(() => this.income()?.monthlyComparison ?? []);

  protected readonly chartOptions = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    legend: { top: 0, data: ['Income', 'Spending'] },
    grid: { left: 24, right: 24, top: 48, bottom: 24, containLabel: true },
    xAxis: { type: 'category', data: this.months().map((month) => month.label) },
    yAxis: {
      type: 'value',
      axisLabel: {
        formatter: (value: number) => this.currencyFormatter().format(value)
      }
    },
    series: [
      {
        name: 'Income',
        type: 'bar',
        data: this.months().map((month) => month.income),
        itemStyle: { color: '#10b981' },
        barMaxWidth: 36
      },
      {
        name: 'Spending',
        type: 'bar',
        data: this.months().map((month) => month.spending),
        itemStyle: { color: '#ef4444' },
        barMaxWidth: 36
      }
    ]
  }));
}

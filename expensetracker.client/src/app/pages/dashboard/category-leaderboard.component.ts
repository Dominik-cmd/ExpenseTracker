import { CommonModule } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { CardModule } from 'primeng/card';
import { EChartsOption } from 'echarts';
import { NgxEchartsDirective } from 'ngx-echarts';

import { CategoryBreakdown } from '../../core/models';
import { CurrencyService } from '../../core/services/currency.service';
import { AppCurrencyPipe } from '../../shared';

@Component({
  standalone: true,
  selector: 'app-category-leaderboard',
  imports: [CommonModule, CardModule, NgxEchartsDirective, AppCurrencyPipe],
  template: `
    <p-card>
      <div class="widget-header">
        <div>
          <div class="widget-title">Spending this month</div>
          <div class="widget-subtitle">Top 8 categories</div>
        </div>
        <span class="leaderboard-total">{{ total() | appCurrency:'1.0-0' }}</span>
      </div>

      @if (leaderboard().length > 0) {
        <div echarts [options]="chartOptions()" class="leaderboard-chart" [style.height.px]="chartHeight()"></div>
      } @else {
        <div class="empty-state">No spending recorded yet this month.</div>
      }
    </p-card>
  `,
  styles: [`
    :host {
      display: block;
      flex: 1;
    }

    .widget-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
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

    .leaderboard-total {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-color-secondary);
      white-space: nowrap;
    }

    .leaderboard-chart {
      width: 100%;
    }

    .empty-state {
      padding: 1.5rem 0;
      text-align: center;
      color: var(--text-color-secondary);
      font-size: 0.875rem;
    }
  `]
})
export class CategoryLeaderboardComponent {
  readonly categories = input<CategoryBreakdown[]>([]);

  private readonly currencyService = inject(CurrencyService);

  private readonly wholeCurrencyFormatter = computed(() => new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: this.currencyService.currency(),
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  }));

  private readonly preciseCurrencyFormatter = computed(() => new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: this.currencyService.currency(),
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }));

  protected readonly leaderboard = computed(() => {
    const items = [...this.categories()].sort((a, b) => b.amount - a.amount);
    const top8 = items.slice(0, 8);
    const rest = items.slice(8);

    if (rest.length > 0) {
      top8.push({
        name: `Other (${rest.length} categories)`,
        color: '#94a3b8',
        amount: rest.reduce((sum, item) => sum + item.amount, 0),
        percentage: rest.reduce((sum, item) => sum + item.percentage, 0)
      });
    }

    return top8;
  });

  protected readonly total = computed(() =>
    this.categories().reduce((sum, item) => sum + item.amount, 0)
  );

  protected readonly chartHeight = computed(() => Math.max(260, this.leaderboard().length * 42 + 48));

  protected readonly chartOptions = computed<EChartsOption>(() => {
    const items = this.leaderboard();

    return {
      animationDuration: 300,
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        formatter: (params: { dataIndex: number } | Array<{ dataIndex: number }>) => {
          const point = Array.isArray(params) ? params[0] : params;
          const item = items[point.dataIndex];
          return `${item.name}<br>${this.formatAmount(item.amount)} · ${item.percentage.toFixed(1)}%`;
        }
      },
      grid: { left: 8, right: 124, top: 8, bottom: 8, containLabel: true },
      xAxis: {
        type: 'value',
        axisLabel: {
          formatter: (value: number) => this.wholeCurrencyFormatter().format(value)
        },
        splitLine: {
          lineStyle: { color: '#e2e8f0' }
        }
      },
      yAxis: {
        type: 'category',
        inverse: true,
        axisTick: { show: false },
        axisLine: { show: false },
        data: items.map((item) => item.name)
      },
      series: [
        {
          type: 'bar',
          barMaxWidth: 18,
          label: {
            show: true,
            position: 'right',
            color: '#64748b',
            fontSize: 12,
            formatter: ({ dataIndex }: { dataIndex: number }) => {
              const item = items[dataIndex];
              return `${this.formatAmount(item.amount)}  ${item.percentage.toFixed(1)}%`;
            }
          },
          data: items.map((item) => ({
            value: item.amount,
            itemStyle: {
              color: item.color || '#94a3b8',
              borderRadius: [0, 8, 8, 0]
            }
          }))
        }
      ]
    };
  });

  private formatAmount(amount: number): string {
    return amount >= 100
      ? this.wholeCurrencyFormatter().format(amount)
      : this.preciseCurrencyFormatter().format(amount);
  }
}

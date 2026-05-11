import { CommonModule } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { EChartsOption } from 'echarts';
import { NgxEchartsDirective } from 'ngx-echarts';
import { CardModule } from 'primeng/card';

import { HistoryPoint } from '../../core/models';

interface HistoryRangeOption {
  label: string;
  months: number;
}

export interface HistoryRangeChange {
  label: string;
  from?: string;
  to?: string;
}

const HISTORY_RANGES: HistoryRangeOption[] = [
  { label: '1M', months: 1 },
  { label: '3M', months: 3 },
  { label: 'YTD', months: 0 },
  { label: '1Y', months: 12 },
  { label: 'All', months: -1 }
];

@Component({
  selector: 'app-portfolio-history-chart',
  standalone: true,
  imports: [CommonModule, CardModule, NgxEchartsDirective],
  template: `
    <p-card>
      <ng-template #header>
        <div class="widget-header">
          <span>Portfolio value</span>
          <div class="range-toggle">
            @for (range of historyRanges; track range.label) {
              <button
                type="button"
                class="range-btn"
                [class.active]="selectedRange() === range.label"
                (click)="changeRange(range)">
                {{ range.label }}
              </button>
            }
          </div>
        </div>
      </ng-template>

      @if (data().length > 0) {
        <div echarts [options]="chartOptions()" class="portfolio-chart"></div>
      } @else {
        <div class="empty-state">No portfolio history available.</div>
      }
    </p-card>
  `,
  styles: [`
    .widget-header { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; font-weight: 600; gap: 0.75rem; flex-wrap: wrap; }
    .range-toggle { display: flex; gap: 0.25rem; flex-wrap: wrap; }
    .range-btn { background: none; border: 1px solid var(--app-text-muted); border-radius: 4px; padding: 0.2rem 0.5rem; font-size: 0.75rem; cursor: pointer; color: var(--app-text-color); }
    .range-btn.active { background: var(--p-primary-color); color: #fff; border-color: var(--p-primary-color); }
    .portfolio-chart { width: 100%; height: 300px; }
    .empty-state { padding: 2rem; text-align: center; color: var(--app-text-muted); font-style: italic; }
  `]
})
export class PortfolioHistoryChartComponent {
  readonly data = input<HistoryPoint[]>([]);
  readonly rangeChange = output<HistoryRangeChange>();

  protected readonly historyRanges = HISTORY_RANGES;
  protected readonly selectedRange = signal('All');
  protected readonly chartOptions = computed<EChartsOption>(() => {
    const points = this.data();
    return {
      grid: { left: 60, right: 20, top: 20, bottom: 30 },
      xAxis: { type: 'category', data: points.map((point) => point.date), axisLabel: { fontSize: 11 } },
      yAxis: { type: 'value', axisLabel: { formatter: (value: number) => `€${(value / 1000).toFixed(0)}k` } },
      tooltip: {
        trigger: 'axis',
        formatter: (params: any) => {
          const point = Array.isArray(params) ? params[0] : params;
          return `${point.name}<br/>€${Number(point.value).toLocaleString('en', { maximumFractionDigits: 0 })}`;
        }
      },
      series: [
        {
          type: 'line',
          data: points.map((point) => point.value),
          smooth: true,
          areaStyle: { opacity: 0.15 },
          lineStyle: { width: 2 },
          symbol: 'none'
        }
      ]
    };
  });

  protected changeRange(range: HistoryRangeOption): void {
    this.selectedRange.set(range.label);
    this.rangeChange.emit(this.toRangeChange(range));
  }

  private toRangeChange(range: HistoryRangeOption): HistoryRangeChange {
    if (range.months === -1) {
      return { label: range.label };
    }

    const now = new Date();
    const to = now.toISOString().split('T')[0];
    let from: string | undefined;

    if (range.months === 0) {
      from = `${now.getFullYear()}-01-01`;
    } else {
      const date = new Date(now);
      date.setMonth(date.getMonth() - range.months);
      from = date.toISOString().split('T')[0];
    }

    return { label: range.label, from, to };
  }
}

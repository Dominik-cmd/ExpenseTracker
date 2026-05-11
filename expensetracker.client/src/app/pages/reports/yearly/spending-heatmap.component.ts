import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { EChartsOption } from 'echarts';
import { NgxEchartsDirective } from 'ngx-echarts';

import { CalendarHeatmapPoint } from '../../../core/models';

const EURO_FORMATTER = new Intl.NumberFormat(undefined, {
  style: 'currency',
  currency: 'EUR',
  maximumFractionDigits: 0
});

@Component({
  standalone: true,
  selector: 'app-spending-heatmap',
  imports: [CommonModule, NgxEchartsDirective],
  template: `
    @if (data().length) {
      <div echarts [options]="chartOptions()" class="chart"></div>
    } @else {
      <div class="empty-state">No daily spending data available for {{ year() }}.</div>
    }
  `,
  styles: [`
    .chart { width: 100%; height: 24rem; }

    .empty-state {
      padding: 1.5rem;
      text-align: center;
      color: var(--text-color-secondary);
    }
  `]
})
export class SpendingHeatmapComponent {
  readonly data = input.required<CalendarHeatmapPoint[]>();
  readonly year = input.required<number>();

  protected readonly chartOptions = computed<EChartsOption>(() => {
    const points = this.data();
    const values = points.map((point) => point.amount);
    const max = Math.max(...values, 1);

    return {
      tooltip: {
        position: 'top',
        formatter: (params: unknown) => {
          const [date, amount] = (params?.data ?? []) as [string, number];
          const formattedDate = new Date(`${date}T00:00:00`).toLocaleDateString(undefined, {
            month: 'short',
            day: 'numeric',
            year: 'numeric'
          });
          return `${formattedDate}<br/>${EURO_FORMATTER.format(Number(amount ?? 0))}`;
        }
      },
      visualMap: {
        min: 0,
        max,
        calculable: true,
        orient: 'horizontal',
        left: 'center',
        top: 0,
        inRange: {
          color: ['#ecfeff', '#a5f3fc', '#22d3ee', '#0ea5e9', '#0369a1']
        }
      },
      calendar: {
        top: 60,
        left: 24,
        right: 24,
        cellSize: ['auto', 18],
        range: `${this.year()}`,
        yearLabel: { show: false }
      },
      series: [
        {
          type: 'heatmap',
          coordinateSystem: 'calendar',
          data: points.map((point) => [point.date.slice(0, 10), point.amount])
        }
      ]
    };
  });
}

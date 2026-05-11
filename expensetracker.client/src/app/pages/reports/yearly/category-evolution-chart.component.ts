import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { EChartsOption } from 'echarts';
import { NgxEchartsDirective } from 'ngx-echarts';

import { MonthlyCategorySeries } from '../../../core/models';

const FALLBACK_CATEGORY_COLOR = '#94a3b8';
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

@Component({
  standalone: true,
  selector: 'app-category-evolution-chart',
  imports: [CommonModule, NgxEchartsDirective],
  template: `
    @if (series().length) {
      <div echarts [options]="chartOptions()" class="chart"></div>
    } @else {
      <div class="empty-state">No category evolution data available.</div>
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
export class CategoryEvolutionChartComponent {
  readonly series = input.required<MonthlyCategorySeries[]>();
  readonly categoryColorMap = input<Map<string, string> | null>(null);

  protected readonly chartOptions = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'axis' },
    legend: {
      top: 0,
      type: 'scroll',
      data: this.series().map((entry) => entry.categoryName),
      textStyle: { fontSize: 11 }
    },
    grid: { left: 24, right: 24, top: 56, bottom: 24, containLabel: true },
    xAxis: { type: 'category', data: MONTH_LABELS },
    yAxis: { type: 'value' },
    series: this.series().map((entry) => {
      const color = this.resolveCategoryColor(entry.categoryName, entry.color);
      const valuesByMonth = new Map(entry.values.map((value) => [value.month, value.amount]));

      return {
        name: entry.categoryName,
        type: 'line',
        stack: 'total',
        smooth: true,
        emphasis: { focus: 'series' },
        symbol: 'circle',
        lineStyle: { color, width: 2 },
        itemStyle: { color },
        areaStyle: { color, opacity: 0.18 },
        data: MONTH_LABELS.map((_, index) => valuesByMonth.get(index + 1) ?? 0)
      };
    })
  }));

  private resolveCategoryColor(name: string, fallback?: string | null): string {
    return this.categoryColorMap()?.get(name) ?? fallback ?? FALLBACK_CATEGORY_COLOR;
  }
}

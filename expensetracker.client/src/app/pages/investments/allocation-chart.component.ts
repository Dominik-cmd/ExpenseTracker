import { CommonModule } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EChartsOption } from 'echarts';
import { NgxEchartsDirective } from 'ngx-echarts';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';

import { AllocationBreakdown } from '../../core/models';
import { AppCurrencyPipe } from '../../shared';

const DEFAULT_ALLOCATION: AllocationBreakdown = {
  allocationType: 'accountType',
  totalValue: 0,
  slices: []
};

const ALLOCATION_TYPES = [
  { label: 'Account type', value: 'accountType' },
  { label: 'Asset class', value: 'assetClass' },
  { label: 'Account', value: 'account' },
  { label: 'Currency', value: 'currency' }
];

@Component({
  selector: 'app-allocation-chart',
  standalone: true,
  imports: [CommonModule, FormsModule, CardModule, SelectModule, NgxEchartsDirective, AppCurrencyPipe],
  template: `
    <p-card>
      <ng-template #header>
        <div class="widget-header">
          <span>Allocation</span>
          <p-select
            [ngModel]="selectedAllocationType()"
            (ngModelChange)="changeAllocationType($event)"
            [options]="allocationTypes"
            optionLabel="label"
            optionValue="value"
            placeholder="Allocation type" />
        </div>
      </ng-template>

      @if (allocation().slices.length > 0) {
        <div echarts [options]="chartOptions()" class="allocation-chart"></div>
        @for (slice of allocation().slices; track slice.label) {
          <div class="alloc-row">
            <span class="alloc-label">{{ slice.label }}</span>
            <span class="alloc-value">{{ slice.value | appCurrency:'1.0-0' }}</span>
            <span class="alloc-pct">{{ slice.percentage | number:'1.0-1' }}%</span>
            <div class="alloc-bar-bg"><div class="alloc-bar" [style.width.%]="slice.percentage"></div></div>
          </div>
        }
      } @else {
        <div class="empty-state">No allocation data available.</div>
      }
    </p-card>
  `,
  styles: [`
    .widget-header { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; font-weight: 600; gap: 0.75rem; flex-wrap: wrap; }
    .allocation-chart { width: 100%; height: 320px; }
    .alloc-row { display: flex; align-items: center; gap: 0.5rem; padding: 0.5rem 1rem; }
    .alloc-label { flex: 1; font-size: 0.875rem; }
    .alloc-value { font-weight: 600; font-size: 0.875rem; min-width: 80px; text-align: right; }
    .alloc-pct { font-size: 0.75rem; color: var(--app-text-muted); min-width: 40px; text-align: right; }
    .alloc-bar-bg { width: 100px; height: 6px; background: var(--p-content-border-color); border-radius: 3px; overflow: hidden; }
    .alloc-bar { height: 100%; background: var(--p-primary-color); border-radius: 3px; }
    .empty-state { padding: 2rem; text-align: center; color: var(--app-text-muted); font-style: italic; }
  `]
})
export class AllocationChartComponent {
  readonly allocation = input<AllocationBreakdown>(DEFAULT_ALLOCATION);
  readonly allocationTypeChange = output<string>();

  protected readonly allocationTypes = ALLOCATION_TYPES;
  protected readonly selectedAllocationType = signal('accountType');
  protected readonly chartOptions = computed<EChartsOption>(() => ({
    tooltip: {
      trigger: 'item',
      formatter: (params: any) => `${params.name}<br/>€${Number(params.value).toLocaleString('en', { maximumFractionDigits: 0 })} (${params.percent}%)`
    },
    legend: { bottom: 0, type: 'scroll' },
    series: [
      {
        type: 'pie',
        radius: ['45%', '72%'],
        center: ['50%', '45%'],
        label: { formatter: '{b|{b}}\n{d}%', rich: { b: { fontSize: 11, fontWeight: 600 } } },
        labelLine: { length: 12, length2: 10 },
        data: this.allocation().slices.map((slice) => ({ name: slice.label, value: slice.value }))
      }
    ]
  }));

  constructor() {
    effect(() => {
      this.selectedAllocationType.set(this.allocation().allocationType || 'accountType');
    }, { allowSignalWrites: true });
  }

  protected changeAllocationType(type: string | null): void {
    const nextType = type ?? 'accountType';
    if (nextType === this.selectedAllocationType()) {
      return;
    }

    this.selectedAllocationType.set(nextType);
    this.allocationTypeChange.emit(nextType);
  }
}

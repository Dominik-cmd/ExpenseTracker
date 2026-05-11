import { CommonModule } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';

import { MonthlyCategoryTotal, MonthlyCategorySeries } from '../../../core/models';
import { CurrencyService } from '../../../core/services/currency.service';

const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const FALLBACK_COLORS = [
  '#6366f1', '#f59e0b', '#10b981', '#ef4444', '#3b82f6',
  '#8b5cf6', '#ec4899', '#14b8a6', '#f97316', '#06b6d4'
];

interface GridCell {
  amount: number;
  opacity: number;
  color: string;
  formattedAmount: string;
}

@Component({
  standalone: true,
  selector: 'app-month-category-grid',
  imports: [CommonModule],
  template: `
    @if (columns().length > 0) {
      <div class="grid-subtitle">
        Top {{ columns().length }} categories by annual spend · colour intensity = relative share within each column
      </div>
      <div class="grid-scroll-wrapper">
        <table class="mc-grid">
          <thead>
            <tr>
              <th class="row-header"></th>
              @for (col of columns(); track col.name) {
                <th class="col-header">{{ col.name }}</th>
              }
              <th class="col-header col-total">Total</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.month) {
              <tr>
                <td class="row-label">{{ row.label }}</td>
                @for (cell of row.cells; track $index) {
                  <td class="mc-cell"
                      [style.background-color]="cell.color"
                      [style.opacity]="cell.amount > 0 ? 1 : 0.3">
                    <span class="cell-inner" [style.background-color]="cell.color" [style.--cell-opacity]="cell.opacity">
                      {{ cell.formattedAmount }}
                    </span>
                  </td>
                }
                <td class="mc-cell total-cell">{{ row.formattedTotal }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <div class="empty-state">No month × category data available.</div>
    }
  `,
  styles: [`
    .grid-subtitle {
      font-size: 0.8rem;
      color: var(--text-color-secondary);
      margin-bottom: 1rem;
    }

    .grid-scroll-wrapper {
      overflow-x: auto;
      -webkit-overflow-scrolling: touch;
    }

    .mc-grid {
      width: 100%;
      border-collapse: separate;
      border-spacing: 3px;
      font-size: 0.8rem;
    }

    .col-header {
      text-align: center;
      font-weight: 600;
      font-size: 0.78rem;
      padding: 0.4rem 0.5rem;
      color: var(--text-color);
      white-space: nowrap;
    }

    .col-total {
      min-width: 64px;
    }

    .row-label {
      font-weight: 500;
      font-size: 0.8rem;
      padding: 0.4rem 0.6rem;
      color: var(--text-color-secondary);
      white-space: nowrap;
      text-align: right;
    }

    .mc-cell {
      text-align: center;
      padding: 0;
      border-radius: 5px;
      position: relative;
      min-width: 60px;
    }

    .cell-inner {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 0.4rem 0.35rem;
      border-radius: 5px;
      font-weight: 500;
      font-size: 0.78rem;
      color: #fff;
      opacity: var(--cell-opacity, 0.15);
      min-height: 30px;
      white-space: nowrap;
    }

    .total-cell {
      font-weight: 600;
      font-size: 0.8rem;
      color: var(--text-color);
      background: transparent !important;
      padding: 0.4rem 0.5rem;
    }

    .row-header {
      min-width: 40px;
    }

    .empty-state {
      padding: 1.5rem;
      text-align: center;
      color: var(--text-color-secondary);
    }
  `]
})
export class MonthCategoryGridComponent {
  readonly monthlyCategories = input.required<MonthlyCategoryTotal[]>();
  readonly categoryEvolution = input<MonthlyCategorySeries[]>([]);

  private readonly currencyService = inject(CurrencyService);

  private readonly currencyFormatter = computed(() => new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: this.currencyService.currency(),
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  }));

  /** Top 8 categories ranked by total annual spend */
  protected readonly columns = computed(() => {
    const data = this.monthlyCategories();
    const colorMap = this.buildColorMap();

    // Sum totals per category
    const totals = new Map<string, number>();
    for (const entry of data) {
      totals.set(entry.categoryName, (totals.get(entry.categoryName) ?? 0) + entry.amount);
    }

    // Top 8
    return [...totals.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 8)
      .map(([name], index) => ({
        name,
        color: colorMap.get(name) ?? FALLBACK_COLORS[index % FALLBACK_COLORS.length]
      }));
  });

  /** One row per month that actually has data */
  protected readonly rows = computed(() => {
    const data = this.monthlyCategories();
    const cols = this.columns();
    const fmt = this.currencyFormatter();

    if (cols.length === 0) return [];

    // Build lookup: month -> categoryName -> amount
    const lookup = new Map<number, Map<string, number>>();
    for (const entry of data) {
      if (!lookup.has(entry.month)) lookup.set(entry.month, new Map());
      lookup.get(entry.month)!.set(entry.categoryName, entry.amount);
    }

    // Compute column max for intensity scaling
    const colMaxes = cols.map((col) => {
      let max = 0;
      for (const [, categories] of lookup) {
        const val = categories.get(col.name) ?? 0;
        if (val > max) max = val;
      }
      return max;
    });

    // Only show months that exist in the data
    const months = [...lookup.keys()].sort((a, b) => a - b);

    return months.map((month) => {
      const catMap = lookup.get(month)!;
      let total = 0;

      const cells: GridCell[] = cols.map((col, colIdx) => {
        const amount = catMap.get(col.name) ?? 0;
        total += amount;
        const maxVal = colMaxes[colIdx];
        const opacity = maxVal > 0 ? 0.25 + 0.75 * (amount / maxVal) : 0.15;

        return {
          amount,
          opacity: amount > 0 ? opacity : 0.08,
          color: col.color,
          formattedAmount: amount > 0 ? fmt.format(amount) : ''
        };
      });

      return {
        month,
        label: MONTH_LABELS[month - 1] ?? `M${month}`,
        cells,
        formattedTotal: fmt.format(total)
      };
    });
  });

  private buildColorMap(): Map<string, string> {
    const map = new Map<string, string>();
    for (const series of this.categoryEvolution()) {
      if (series.color) {
        map.set(series.categoryName, series.color);
      }
    }
    return map;
  }
}

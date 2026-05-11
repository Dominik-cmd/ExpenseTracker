import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { CardModule } from 'primeng/card';

import { NarrativeResponse, PortfolioSummary } from '../../core/models';

const DEFAULT_SUMMARY: PortfolioSummary = {
  totalValue: 0,
  ibkrValue: 0,
  manualValue: 0,
  dayChange: null,
  dayChangePercent: null,
  ytdChange: null,
  ytdChangePercent: null,
  baseCurrency: 'EUR',
  asOf: '',
  oldestManualUpdateDays: null
};

@Component({
  selector: 'app-portfolio-summary',
  standalone: true,
  imports: [CommonModule, CardModule],
  template: `
    <p-card>
      <div class="strip-numbers">
        <span class="strip-item strip-item-primary">
          <span class="strip-label">Total value</span>
          <span class="strip-value">{{ summary().totalValue | currency:summary().baseCurrency:'symbol':'1.0-0' }}</span>
        </span>
        <span class="strip-item">
          <span class="strip-label">Day</span>
          <span
            class="strip-value"
            [class.text-positive]="(summary().dayChange ?? 0) > 0"
            [class.text-negative]="(summary().dayChange ?? 0) < 0">
            {{ formatChange(summary().dayChange, summary().dayChangePercent, summary().baseCurrency) }}
          </span>
        </span>
        <span class="strip-item">
          <span class="strip-label">YTD</span>
          <span
            class="strip-value"
            [class.text-positive]="(summary().ytdChange ?? 0) > 0"
            [class.text-negative]="(summary().ytdChange ?? 0) < 0">
            {{ formatChange(summary().ytdChange, summary().ytdChangePercent, summary().baseCurrency) }}
          </span>
        </span>
        <span class="strip-item">
          <span class="strip-label">IBKR</span>
          <span class="strip-value">{{ summary().ibkrValue | currency:summary().baseCurrency:'symbol':'1.0-0' }}</span>
        </span>
        <span class="strip-item">
          <span class="strip-label">Manual</span>
          <span class="strip-value">{{ summary().manualValue | currency:summary().baseCurrency:'symbol':'1.0-0' }}</span>
        </span>
      </div>
      @if (summary().asOf) {
        <div class="as-of">As of {{ summary().asOf | date:'dd MMM y, HH:mm' }}</div>
      }
      @if (narrative()?.content) {
        <div class="strip-narrative">{{ narrative()?.content }}</div>
      }
      @if ((summary().oldestManualUpdateDays ?? 0) > 30) {
        <div class="stale-warning">
          <i class="pi pi-exclamation-triangle"></i>
          Some manual balances haven't been updated in over a month.
          <a (click)="manualAccountsRequested.emit()">Update them →</a>
        </div>
      }
    </p-card>
  `,
  styles: [`
    .strip-numbers { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 0.75rem; }
    .strip-item { display: flex; flex-direction: column; gap: 0.2rem; padding: 0.75rem; border-radius: 10px; background: var(--p-content-hover-background); }
    .strip-item-primary { background: color-mix(in srgb, var(--p-primary-color) 10%, transparent); }
    .strip-label { font-size: 0.75rem; color: var(--app-text-muted); text-transform: uppercase; letter-spacing: 0.05em; }
    .strip-value { font-size: 1.25rem; font-weight: 600; }
    .as-of { margin-top: 0.75rem; color: var(--app-text-muted); font-size: 0.85rem; }
    .strip-narrative { font-style: italic; color: var(--app-text-muted); margin-top: 0.5rem; font-size: 0.875rem; }
    .stale-warning { margin-top: 0.75rem; color: #f59e0b; font-size: 0.85rem; }
    .stale-warning a { cursor: pointer; text-decoration: underline; margin-left: 0.25rem; }
    .text-positive { color: #22c55e !important; }
    .text-negative { color: #ef4444 !important; }
  `]
})
export class PortfolioSummaryComponent {
  readonly summary = input<PortfolioSummary>(DEFAULT_SUMMARY);
  readonly narrative = input<NarrativeResponse | null>(null);
  readonly manualAccountsRequested = output<void>();

  protected formatChange(amount: number | null, percent: number | null, currency: string): string {
    if (amount === null) {
      return '—';
    }

    const sign = amount >= 0 ? '+' : '-';
    const formattedAmount = new Intl.NumberFormat('en', {
      style: 'currency',
      currency,
      maximumFractionDigits: 0
    }).format(Math.abs(amount));
    const pct = percent !== null ? ` (${sign}${percent.toFixed(1)}%)` : '';

    return `${sign}${formattedAmount}${pct}`;
  }
}

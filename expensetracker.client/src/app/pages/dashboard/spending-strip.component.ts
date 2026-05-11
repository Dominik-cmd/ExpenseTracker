import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';

import { DashboardStrip, InvestmentDashboardStrip } from '../../core/models';
import { AppCurrencyPipe } from '../../shared';

@Component({
  standalone: true,
  selector: 'app-spending-strip',
  imports: [CommonModule, CardModule, RouterLink, AppCurrencyPipe],
  template: `
    <p-card>
      <div class="strip-numbers">
        <span class="strip-item">
          <span class="strip-label">This month</span>
          <span class="strip-value">{{ strip().monthToDate | appCurrency:'1.0-0' }}</span>
        </span>
        <span class="strip-sep">·</span>
        <span class="strip-item">
          <span class="strip-label">On pace</span>
          <span class="strip-value">{{ strip().onPace | appCurrency:'1.0-0' }}</span>
        </span>
        <span class="strip-sep">·</span>
        <span class="strip-item">
          <span class="strip-label">Net 30d</span>
          <span class="strip-value" [class.text-positive]="strip().netLast30 > 0" [class.text-negative]="strip().netLast30 < 0">
            {{ strip().netLast30 > 0 ? '+' : '' }}{{ strip().netLast30 | appCurrency:'1.0-0' }}
          </span>
        </span>
      </div>
    </p-card>

    @if (investmentStrip()?.hasData) {
      <p-card styleClass="investment-strip-card">
        <div class="investment-strip" routerLink="/investments">
          <div class="strip-numbers compact-strip">
            <span class="strip-item">
              <span class="strip-label">Portfolio</span>
              <span class="strip-value">{{ investmentStrip()!.totalValue | appCurrency:'1.0-0' }}</span>
            </span>
            <span class="strip-sep">·</span>
            <span class="strip-item">
              <span class="strip-label">Today</span>
              <span
                class="strip-value"
                [class.text-positive]="(investmentStrip()!.dayChange ?? 0) > 0"
                [class.text-negative]="(investmentStrip()!.dayChange ?? 0) < 0">
                @if (investmentStrip()!.dayChange !== null) {
                  {{ investmentStrip()!.dayChange! >= 0 ? '+' : '' }}{{ investmentStrip()!.dayChange | appCurrency:'1.0-0' }}
                  ({{ investmentStrip()!.dayChangePercent! >= 0 ? '+' : '' }}{{ investmentStrip()!.dayChangePercent | number:'1.1-1' }}%)
                } @else {
                  —
                }
              </span>
            </span>
            <span class="strip-sep">·</span>
            <span class="strip-item">
              <span class="strip-label">YTD</span>
              <span
                class="strip-value"
                [class.text-positive]="(investmentStrip()!.ytdChange ?? 0) > 0"
                [class.text-negative]="(investmentStrip()!.ytdChange ?? 0) < 0">
                @if (investmentStrip()!.ytdChange !== null) {
                  {{ investmentStrip()!.ytdChange! >= 0 ? '+' : '' }}{{ investmentStrip()!.ytdChange | appCurrency:'1.0-0' }}
                  ({{ investmentStrip()!.ytdChangePercent! >= 0 ? '+' : '' }}{{ investmentStrip()!.ytdChangePercent | number:'1.1-1' }}%)
                } @else {
                  —
                }
              </span>
            </span>
          </div>
        </div>
      </p-card>
    }
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    .strip-numbers {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.5rem 1rem;
      font-size: 1rem;
    }

    .compact-strip {
      font-size: 0.9rem;
      opacity: 0.85;
    }

    .strip-item {
      display: inline-flex;
      align-items: baseline;
      gap: 0.4rem;
    }

    .strip-label {
      font-size: 0.8rem;
      color: var(--text-color-secondary);
      text-transform: lowercase;
    }

    .strip-value {
      font-size: 1.25rem;
      font-weight: 600;
      color: var(--text-color);
    }

    .strip-sep {
      color: var(--text-color-secondary);
      font-size: 1.1rem;
    }

    .investment-strip {
      cursor: pointer;
    }

    .text-positive {
      color: #10b981;
    }

    .text-negative {
      color: #ef4444;
    }
  `]
})
export class SpendingStripComponent {
  readonly strip = input.required<DashboardStrip>();
  readonly investmentStrip = input<InvestmentDashboardStrip | null>(null);
}

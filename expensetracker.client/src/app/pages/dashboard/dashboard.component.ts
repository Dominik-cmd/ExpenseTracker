import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { CardModule } from 'primeng/card';

import { EMPTY_DASHBOARD_ANALYTICS, EMPTY_DASHBOARD_STRIP } from '../../core/constants/fallbacks';
import {
  DashboardAnalytics,
  DashboardStrip,
  InvestmentDashboardStrip,
  NarrativeResponse,
  Transaction
} from '../../core/models';
import { AnalyticsService } from '../../core/services/analytics.service';
import { InvestmentService } from '../../core/services/investment.service';
import { AppCurrencyPipe } from '../../shared';
import { CategoryLeaderboardComponent } from './category-leaderboard.component';
import { IncomeSpendingChartComponent } from './income-spending-chart.component';
import { NarrativeCardComponent } from './narrative-card.component';
import { SpendingStripComponent } from './spending-strip.component';

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [
    CommonModule,
    CardModule,
    RouterLink,
    AppCurrencyPipe,
    SpendingStripComponent,
    NarrativeCardComponent,
    CategoryLeaderboardComponent,
    IncomeSpendingChartComponent
  ],
  template: `
    <div class="dashboard-root">
      <app-spending-strip [strip]="strip()" [investmentStrip]="investmentStrip()"></app-spending-strip>

      @if (showNarrative()) {
        <app-narrative-card [narrative]="narrative()" [loading]="loading()" title="AI takeaway"></app-narrative-card>
      }

      <div class="main-grid">
        <div class="fill-card-wrapper">
          <p-card>
            <div class="widget-header">
              <span class="widget-title">Recent transactions</span>
              <a class="see-all-link" routerLink="/transactions">See all →</a>
            </div>

            @if ((analytics()?.recentTransactions?.length ?? 0) > 0) {
              <div class="txn-list">
                @for (transaction of (analytics()?.recentTransactions ?? []).slice(0, 10); track transaction.id) {
                  <div class="txn-row">
                    <span class="txn-dot" [style.background]="getCategoryColor(transaction)"></span>
                    <span class="txn-date">{{ transaction.transactionDate | date:'dd MMM' }}</span>
                    <span class="txn-merchant">{{ (transaction.merchantNormalized || transaction.merchantRaw || 'Manual entry') | slice:0:28 }}</span>
                    <span
                      class="txn-amount"
                      [class.text-positive]="transaction.direction === 'Credit'"
                      [class.text-negative]="transaction.direction !== 'Credit'">
                      {{ transaction.direction === 'Credit' ? '+' : '−' }}{{ abs(transaction.amount) | appCurrency:'1.2-2' }}
                    </span>
                  </div>
                }
              </div>
            } @else {
              <div class="empty-state">No transactions yet. Send a test SMS to verify the webhook.</div>
            }
          </p-card>
        </div>

        <div class="fill-card-wrapper">
          <app-category-leaderboard [categories]="analytics()?.categoryLeaderboard ?? []"></app-category-leaderboard>
        </div>
      </div>

      <app-income-spending-chart [income]="analytics()?.income ?? null"></app-income-spending-chart>
    </div>
  `,
  styles: [`
    .dashboard-root { display: flex; flex-direction: column; gap: 1.25rem; }
    .main-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; align-items: stretch; }
    @media (max-width: 768px) { .main-grid { grid-template-columns: 1fr; } }
    .fill-card-wrapper { display: flex; flex-direction: column; }
    .fill-card-wrapper ::ng-deep .p-card,
    .fill-card-wrapper ::ng-deep .p-card-body,
    .fill-card-wrapper ::ng-deep .p-card-content { flex: 1; display: flex; flex-direction: column; }
    .fill-card-wrapper ::ng-deep .p-card-content { padding-top: 0; }
    .widget-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1rem; }
    .widget-title { font-size: 1rem; font-weight: 600; color: var(--text-color); }
    .see-all-link { font-size: 0.8rem; color: var(--primary-color); text-decoration: none; white-space: nowrap; }
    .see-all-link:hover { text-decoration: underline; }
    .txn-list { flex: 1; display: flex; flex-direction: column; }
    .txn-row { display: grid; grid-template-columns: 10px 52px 1fr auto; align-items: center; gap: 0.6rem; padding: 0.55rem 0; border-bottom: 1px solid var(--surface-border); }
    .txn-row:last-child { border-bottom: none; }
    .txn-dot { width: 9px; height: 9px; border-radius: 50%; flex-shrink: 0; }
    .txn-date { font-size: 0.78rem; color: var(--text-color-secondary); white-space: nowrap; }
    .txn-merchant { font-size: 0.875rem; color: var(--text-color); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .txn-amount { font-size: 0.875rem; font-weight: 500; white-space: nowrap; text-align: right; }
    .text-positive { color: #10b981; }
    .text-negative { color: #ef4444; }
    .empty-state { padding: 1.5rem 0; text-align: center; color: var(--text-color-secondary); font-size: 0.875rem; }
  `]
})
export class DashboardComponent {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly investmentService = inject(InvestmentService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly strip = signal<DashboardStrip>(EMPTY_DASHBOARD_STRIP);
  protected readonly investmentStrip = signal<InvestmentDashboardStrip | null>(null);
  protected readonly analytics = signal<DashboardAnalytics | null>(null);
  protected readonly narrative = signal<NarrativeResponse | null>(null);
  protected readonly showNarrative = computed(() =>
    this.loading() || !!this.narrative()?.content || !!this.narrative()?.isStale
  );

  constructor() {
    forkJoin({
      strip: this.analyticsService.getDashboardStrip().pipe(catchError(() => of(EMPTY_DASHBOARD_STRIP))),
      investmentStrip: this.investmentService.getDashboardStrip().pipe(catchError(() => of(null))),
      analytics: this.analyticsService.getDashboardAnalytics().pipe(catchError(() => of(EMPTY_DASHBOARD_ANALYTICS))),
      narrative: this.analyticsService.getDashboardNarrative().pipe(catchError(() => of(null)))
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ strip, investmentStrip, analytics, narrative }) => {
      this.strip.set(strip);
      this.investmentStrip.set(investmentStrip);
      this.analytics.set(analytics);
      this.narrative.set(narrative);
      this.loading.set(false);
    });
  }

  protected abs(value: number | null | undefined): number {
    return Math.abs(value ?? 0);
  }

  protected getCategoryColor(transaction: Transaction): string {
    const leaderboard = this.analytics()?.categoryLeaderboard ?? [];
    const match = leaderboard.find((item) => item.name === (transaction.parentCategoryName || transaction.categoryName));
    return match?.color || '#94a3b8';
  }
}

import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of } from 'rxjs';
import { CardModule } from 'primeng/card';

import {
  AccountSummary,
  AllocationBreakdown,
  HistoryPoint,
  ManualAccount,
  NarrativeResponse,
  PortfolioSummary,
  RecentActivity
} from '../../core/models';
import { InvestmentService } from '../../core/services/investment.service';
import { AppCurrencyPipe } from '../../shared';
import { AllocationChartComponent } from './allocation-chart.component';
import {
  BalanceUpdateRequest,
  CreateManualAccountRequest,
  DeleteManualAccountRequest,
  ManualAccountsComponent,
  UpdateManualAccountRequest
} from './manual-accounts.component';
import { HistoryRangeChange, PortfolioHistoryChartComponent } from './portfolio-history-chart.component';
import { PortfolioSummaryComponent } from './portfolio-summary.component';

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

const DEFAULT_ALLOCATION: AllocationBreakdown = {
  allocationType: 'accountType',
  totalValue: 0,
  slices: []
};

@Component({
  standalone: true,
  selector: 'app-investments',
  imports: [
    CommonModule,
    AppCurrencyPipe,
    CardModule,
    PortfolioSummaryComponent,
    AllocationChartComponent,
    PortfolioHistoryChartComponent,
    ManualAccountsComponent
  ],
  template: `
    <div class="investments-root">
      <app-portfolio-summary
        [summary]="summary()"
        [narrative]="narrative()"
        (manualAccountsRequested)="scrollToManual()" />

      <div class="invest-grid">
        <p-card>
          <ng-template #header>
            <div class="widget-header">
              <span>Accounts <span class="text-muted">({{ accounts().length }})</span></span>
              <span class="text-muted">{{ summary().totalValue | appCurrency:'1.0-0' }}</span>
            </div>
          </ng-template>

          @for (account of accounts(); track account.accountId) {
            <div class="account-row" (click)="onAccountClick(account)">
              <span class="account-dot" [style.background]="account.color"></span>
              <span class="account-info">
                <span class="account-name">{{ account.displayName }}</span>
                <span class="account-sub">{{ account.accountType }} · {{ account.providerType === 'ibkr' ? 'IBKR' : 'Manual' }}</span>
              </span>
              <span class="account-value-col">
                <span class="account-value">{{ account.value | appCurrency:'1.0-0' }}</span>
                @if (account.providerType === 'manual' && account.daysSinceUpdate !== null) {
                  <span class="account-updated" [class.stale]="account.daysSinceUpdate > 30">
                    Updated {{ account.daysSinceUpdate }}d ago
                    @if (account.daysSinceUpdate > 30) { <i class="pi pi-exclamation-triangle"></i> }
                  </span>
                }
              </span>
            </div>
          }

          @if (accounts().length === 0) {
            <div class="empty-state">No accounts yet. Add a manual account or configure IBKR.</div>
          }
        </p-card>

        <app-allocation-chart
          [allocation]="allocation()"
          (allocationTypeChange)="changeAllocationType($event)" />
      </div>

      <app-portfolio-history-chart
        [data]="history()"
        (rangeChange)="changeHistoryRange($event)" />

      <p-card>
        <ng-template #header>
          <div class="widget-header"><span>Recent activity</span></div>
        </ng-template>

        @for (item of activity(); track item.date + item.activityType + item.accountId) {
          <div class="activity-row">
            <span class="activity-date">{{ item.date | date:'dd MMM' }}</span>
            <span class="activity-type" [class]="'activity-type type-' + item.activityType.toLowerCase()">{{ item.activityType }}</span>
            <span class="activity-desc">{{ item.instrumentSymbol || item.accountDisplayName }}</span>
            @if (item.quantity) {
              <span class="activity-qty">{{ item.quantity }} &#64; {{ item.amount ? (item.amount / item.quantity | appCurrency:'1.2-2') : '—' }}</span>
            }
            <span class="activity-amount" [class.text-positive]="(item.amount ?? 0) > 0" [class.text-negative]="(item.amount ?? 0) < 0">
              {{ item.amount !== null ? (item.amount | appCurrency:'1.2-2') : '—' }}
            </span>
          </div>
        }

        @if (activity().length === 0) {
          <div class="empty-state">No recent activity.</div>
        }
      </p-card>

      <div id="manual-accounts">
        <app-manual-accounts
          [accounts]="manualAccounts()"
          (accountCreated)="createAccount($event)"
          (accountUpdated)="updateAccount($event)"
          (accountDeleted)="deleteAccount($event)"
          (balanceUpdated)="updateBalance($event)" />
      </div>
    </div>
  `,
  styles: [`
    .investments-root { display: flex; flex-direction: column; gap: 1rem; padding: 1rem; }
    .invest-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    @media (max-width: 768px) { .invest-grid { grid-template-columns: 1fr; } }

    .widget-header { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; font-weight: 600; gap: 0.75rem; flex-wrap: wrap; }
    .text-positive { color: #22c55e !important; }
    .text-negative { color: #ef4444 !important; }
    .text-muted { color: var(--app-text-muted); font-size: 0.875rem; }

    .account-row { display: flex; align-items: center; gap: 0.75rem; padding: 0.6rem 1rem; cursor: pointer; border-bottom: 1px solid var(--p-content-border-color); }
    .account-row:hover { background: var(--p-content-hover-background); }
    .account-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
    .account-info { display: flex; flex-direction: column; flex: 1; min-width: 0; }
    .account-name { font-weight: 600; font-size: 0.9rem; }
    .account-sub { font-size: 0.75rem; color: var(--app-text-muted); }
    .account-value-col { text-align: right; display: flex; flex-direction: column; }
    .account-value { font-weight: 600; }
    .account-updated { font-size: 0.7rem; color: var(--app-text-muted); }
    .account-updated.stale { color: #f59e0b; }

    .activity-row { display: flex; align-items: center; gap: 0.75rem; padding: 0.5rem 1rem; border-bottom: 1px solid var(--p-content-border-color); font-size: 0.875rem; flex-wrap: wrap; }
    .activity-date { color: var(--app-text-muted); min-width: 50px; }
    .activity-type { font-weight: 600; min-width: 60px; text-transform: uppercase; font-size: 0.75rem; }
    .type-buy { color: #22c55e; }
    .type-sell { color: #ef4444; }
    .type-dividend, .type-div { color: #3b82f6; }
    .type-balance_update { color: #8b5cf6; }
    .activity-desc { flex: 1; }
    .activity-qty { color: var(--app-text-muted); font-size: 0.8rem; }
    .activity-amount { font-weight: 600; min-width: 100px; text-align: right; margin-left: auto; }

    .empty-state { padding: 2rem; text-align: center; color: var(--app-text-muted); font-style: italic; }
  `]
})
export class InvestmentsComponent {
  private readonly investmentService = inject(InvestmentService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly manualAccountsComponent = viewChild(ManualAccountsComponent);

  protected readonly summary = signal<PortfolioSummary>(DEFAULT_SUMMARY);
  protected readonly accounts = signal<AccountSummary[]>([]);
  protected readonly allocation = signal<AllocationBreakdown>(DEFAULT_ALLOCATION);
  protected readonly history = signal<HistoryPoint[]>([]);
  protected readonly activity = signal<RecentActivity[]>([]);
  protected readonly manualAccounts = signal<ManualAccount[]>([]);
  protected readonly narrative = signal<NarrativeResponse | null>(null);

  private readonly selectedAllocationType = signal('accountType');
  private readonly selectedHistoryRange = signal<HistoryRangeChange>({ label: 'All' });

  constructor() {
    this.loadData();
  }

  protected changeAllocationType(type: string): void {
    this.selectedAllocationType.set(type);
    this.investmentService.getAllocation(type)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((allocation) => this.allocation.set(allocation));
  }

  protected changeHistoryRange(range: HistoryRangeChange): void {
    this.selectedHistoryRange.set(range);
    this.investmentService.getPortfolioHistory(range.from, range.to)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((history) => this.history.set(history));
  }

  protected onAccountClick(account: AccountSummary): void {
    if (account.providerType !== 'manual') {
      return;
    }

    const manualAccount = this.manualAccounts().find((item) => item.id === account.accountId);
    if (manualAccount) {
      this.manualAccountsComponent()?.openBalanceDialog(manualAccount);
    }
  }

  protected scrollToManual(): void {
    document.getElementById('manual-accounts')?.scrollIntoView({ behavior: 'smooth' });
  }

  protected createAccount(request: CreateManualAccountRequest): void {
    this.investmentService.createManualAccount(request.payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          request.succeed();
          this.loadData();
        },
        error: () => request.fail()
      });
  }

  protected updateAccount(request: UpdateManualAccountRequest): void {
    this.investmentService.updateManualAccount(request.accountId, request.payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          request.succeed();
          this.loadData();
        },
        error: () => request.fail()
      });
  }

  protected deleteAccount(request: DeleteManualAccountRequest): void {
    this.investmentService.deleteManualAccount(request.accountId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          request.succeed();
          this.loadData();
        },
        error: () => request.fail()
      });
  }

  protected updateBalance(request: BalanceUpdateRequest): void {
    this.investmentService.updateManualBalance(request.accountId, request.payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          request.succeed();
          this.loadData();
        },
        error: () => request.fail()
      });
  }

  private loadData(): void {
    const historyRange = this.selectedHistoryRange();

    forkJoin({
      summary: this.investmentService.getPortfolioSummary().pipe(catchError(() => of(this.summary()))),
      accounts: this.investmentService.getAccounts().pipe(catchError(() => of([]))),
      allocation: this.investmentService.getAllocation(this.selectedAllocationType()).pipe(catchError(() => of(this.allocation()))),
      history: this.investmentService.getPortfolioHistory(historyRange.from, historyRange.to).pipe(catchError(() => of(this.history()))),
      activity: this.investmentService.getActivity(15).pipe(catchError(() => of([]))),
      manualAccounts: this.investmentService.getManualAccounts().pipe(catchError(() => of([]))),
      narrative: this.investmentService.getNarrative().pipe(catchError(() => of(null)))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.summary.set(data.summary);
      this.accounts.set(data.accounts);
      this.allocation.set(data.allocation);
      this.history.set(data.history);
      this.activity.set(data.activity);
      this.manualAccounts.set(data.manualAccounts);
      this.narrative.set(data.narrative);
    });
  }
}

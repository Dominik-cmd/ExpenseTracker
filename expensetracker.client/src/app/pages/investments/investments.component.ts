import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ConfirmationService } from 'primeng/api';
import { NgxEchartsDirective } from 'ngx-echarts';
import { EChartsOption } from 'echarts';

import {
  AccountSummary,
  AllocationBreakdown,
  ApiService,
  HistoryPoint,
  ManualAccount,
  NarrativeResponse,
  PortfolioSummary,
  RecentActivity
} from '../../core/services/api.service';

const ACCOUNT_TYPES = [
  { label: 'Broker', value: 'Broker' },
  { label: 'Savings', value: 'Savings' },
  { label: 'Crypto', value: 'Crypto' },
  { label: 'Cash', value: 'Cash' },
  { label: 'Pension', value: 'Pension' },
  { label: 'Real Estate', value: 'RealEstate' },
  { label: 'Other', value: 'Other' }
];

const ALLOCATION_TYPES = [
  { label: 'Account type', value: 'accountType' },
  { label: 'Asset class', value: 'assetClass' },
  { label: 'Account', value: 'account' },
  { label: 'Currency', value: 'currency' }
];

const HISTORY_RANGES = [
  { label: '1M', months: 1 },
  { label: '3M', months: 3 },
  { label: 'YTD', months: 0 },
  { label: '1Y', months: 12 },
  { label: 'All', months: -1 }
];

@Component({
  standalone: true,
  selector: 'app-investments',
  imports: [
    CommonModule,
    FormsModule,
    CardModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    TagModule,
    NgxEchartsDirective
  ],
  template: `
    <div class="investments-root">
      <p-card>
        <div class="strip-numbers">
          <span class="strip-item">
            <span class="strip-label">Total value</span>
            <span class="strip-value">{{ summary().totalValue | currency:'EUR':'symbol':'1.0-0' }}</span>
          </span>
          <span class="strip-sep">·</span>
          <span class="strip-item">
            <span class="strip-label">YTD</span>
            <span
              class="strip-value"
              [class.text-positive]="(summary().ytdChange ?? 0) > 0"
              [class.text-negative]="(summary().ytdChange ?? 0) < 0">
              {{ formatChange(summary().ytdChange, summary().ytdChangePercent) }}
            </span>
          </span>
          <span class="strip-sep">·</span>
          <span class="strip-item">
            <span class="strip-label">IBKR</span>
            <span class="strip-value">{{ summary().ibkrValue | currency:'EUR':'symbol':'1.0-0' }}</span>
          </span>
          <span class="strip-sep">·</span>
          <span class="strip-item">
            <span class="strip-label">Manual</span>
            <span class="strip-value">{{ summary().manualValue | currency:'EUR':'symbol':'1.0-0' }}</span>
          </span>
        </div>
        @if (narrative()?.content) {
          <div class="strip-narrative">{{ narrative()!.content }}</div>
        }
        @if ((summary().oldestManualUpdateDays ?? 0) > 30) {
          <div class="stale-warning">
            <i class="pi pi-exclamation-triangle"></i>
            Some manual balances haven't been updated in over a month.
            <a (click)="scrollToManual()">Update them →</a>
          </div>
        }
      </p-card>

      <div class="invest-grid">
        <p-card>
          <ng-template #header>
            <div class="widget-header">
              <span>Accounts <span class="text-muted">({{ accounts().length }})</span></span>
              <span class="text-muted">{{ summary().totalValue | currency:'EUR':'symbol':'1.0-0' }}</span>
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
                <span class="account-value">{{ account.value | currency:'EUR':'symbol':'1.0-0' }}</span>
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

        <p-card>
          <ng-template #header>
            <div class="widget-header">
              <span>Allocation</span>
              <div class="alloc-toggle">
                @for (at of allocationTypes; track at.value) {
                  <button
                    class="alloc-btn"
                    [class.active]="selectedAllocationType() === at.value"
                    (click)="changeAllocationType(at.value)">{{ at.label }}</button>
                }
              </div>
            </div>
          </ng-template>
          @for (slice of allocation().slices; track slice.label) {
            <div class="alloc-row">
              <span class="alloc-label">{{ slice.label }}</span>
              <span class="alloc-value">{{ slice.value | currency:'EUR':'symbol':'1.0-0' }}</span>
              <span class="alloc-pct">{{ slice.percentage | number:'1.0-1' }}%</span>
              <div class="alloc-bar-bg"><div class="alloc-bar" [style.width.%]="slice.percentage"></div></div>
            </div>
          }
        </p-card>
      </div>

      <p-card>
        <ng-template #header>
          <div class="widget-header">
            <span>Portfolio value</span>
            <div class="range-toggle">
              @for (r of historyRanges; track r.label) {
                <button
                  class="alloc-btn"
                  [class.active]="selectedRange() === r.label"
                  (click)="changeHistoryRange(r)">{{ r.label }}</button>
              }
            </div>
          </div>
        </ng-template>
        <div echarts [options]="chartOptions()" class="portfolio-chart"></div>
      </p-card>

      <p-card>
        <ng-template #header>
          <div class="widget-header"><span>Recent activity</span></div>
        </ng-template>
        @for (item of activity(); track $index) {
          <div class="activity-row">
            <span class="activity-date">{{ item.date | date:'dd MMM' }}</span>
            <span class="activity-type" [class]="'activity-type type-' + item.activityType.toLowerCase()">{{ item.activityType }}</span>
            <span class="activity-desc">{{ item.instrumentSymbol || item.accountDisplayName }}</span>
            @if (item.quantity) {
              <span class="activity-qty">{{ item.quantity }} &#64; {{ item.amount ? (item.amount / item.quantity | currency:'EUR':'symbol':'1.2-2') : '—' }}</span>
            }
            <span class="activity-amount" [class.text-positive]="(item.amount ?? 0) > 0" [class.text-negative]="(item.amount ?? 0) < 0">
              {{ item.amount !== null ? (item.amount | currency:'EUR':'symbol':'1.2-2') : '—' }}
            </span>
          </div>
        }
        @if (activity().length === 0) {
          <div class="empty-state">No recent activity.</div>
        }
      </p-card>

      <p-card id="manual-accounts">
        <ng-template #header>
          <div class="widget-header">
            <span>Manual accounts</span>
            <p-button label="Add account" icon="pi pi-plus" [text]="true" (onClick)="openAddAccountDialog()" />
          </div>
        </ng-template>
        @for (account of manualAccounts(); track account.id) {
          <div class="manual-row">
            <span class="account-dot" [style.background]="account.color ?? '#607D8B'"></span>
            <span class="account-info">
              <span class="account-name">{{ account.displayName }}</span>
              <span class="account-sub">{{ account.accountType }}</span>
            </span>
            <span class="manual-balance">{{ (account.balance ?? 0) | currency:'EUR':'symbol':'1.0-0' }}</span>
            @if (account.lastUpdated) {
              <span class="account-updated" [class.stale]="daysSince(account.lastUpdated) > 30">
                Updated {{ daysSince(account.lastUpdated) }}d ago
                @if (daysSince(account.lastUpdated) > 30) { <i class="pi pi-exclamation-triangle"></i> }
              </span>
            }
            <p-button label="Balance" icon="pi pi-wallet" [text]="true" size="small" (onClick)="openUpdateBalanceDialog(account)" />
            <p-button icon="pi pi-pencil" [text]="true" size="small" severity="secondary" (onClick)="openEditAccountDialog(account)" />
            <p-button icon="pi pi-trash" [text]="true" size="small" severity="danger" (onClick)="confirmDeleteAccount(account)" />
          </div>
        }
        @if (manualAccounts().length === 0) {
          <div class="empty-state">No manual accounts yet. Click "Add account" to start tracking.</div>
        }
      </p-card>

      <p-dialog
        [header]="'Update ' + (selectedAccount()?.displayName ?? '')"
        [visible]="showUpdateDialog()"
        (visibleChange)="showUpdateDialog.set($event)"
        [modal]="true"
        [style]="{ width: '400px' }">
        <div class="dialog-form">
          <label>Current balance: {{ (selectedAccount()?.balance ?? 0) | currency:'EUR':'symbol':'1.0-0' }}</label>
          <div class="field">
            <label>New balance</label>
            <p-inputNumber [(ngModel)]="newBalance" mode="currency" currency="EUR" locale="en-US" />
          </div>
          <div class="field">
            <label>Note (optional)</label>
            <input pInputText [(ngModel)]="updateNote" placeholder="e.g., Monthly check" />
          </div>
        </div>
        <ng-template #footer>
          <p-button label="Cancel" [text]="true" (onClick)="showUpdateDialog.set(false)" />
          <p-button label="Save" icon="pi pi-check" (onClick)="saveBalance()" [loading]="saving()" />
        </ng-template>
      </p-dialog>

      <p-dialog
        [header]="'Edit ' + (selectedAccount()?.displayName ?? '')"
        [visible]="showEditDialog()"
        (visibleChange)="showEditDialog.set($event)"
        [modal]="true"
        [style]="{ width: '400px' }">
        <div class="dialog-form">
          <div class="field">
            <label>Display name</label>
            <input pInputText [(ngModel)]="editAccountName" placeholder="e.g., NLB Savings" />
          </div>
          <div class="field">
            <label>Account type</label>
            <p-select [(ngModel)]="editAccountType" [options]="accountTypes" optionLabel="label" optionValue="value" />
          </div>
        </div>
        <ng-template #footer>
          <p-button label="Cancel" [text]="true" (onClick)="showEditDialog.set(false)" />
          <p-button label="Save" icon="pi pi-check" (onClick)="saveEditAccount()" [loading]="saving()" />
        </ng-template>
      </p-dialog>

      <p-dialog
        header="Add manual account"
        [visible]="showAddDialog()"
        (visibleChange)="showAddDialog.set($event)"
        [modal]="true"
        [style]="{ width: '450px' }">
        <div class="dialog-form">
          <div class="field">
            <label>Display name</label>
            <input pInputText [(ngModel)]="newAccountName" placeholder="e.g., NLB Savings" />
          </div>
          <div class="field">
            <label>Account type</label>
            <p-select [(ngModel)]="newAccountType" [options]="accountTypes" optionLabel="label" optionValue="value" placeholder="Select type" />
          </div>
          <div class="field">
            <label>Currency</label>
            <input pInputText [(ngModel)]="newAccountCurrency" placeholder="EUR" />
          </div>
          <div class="field">
            <label>Initial balance (optional)</label>
            <p-inputNumber [(ngModel)]="newAccountBalance" mode="currency" currency="EUR" locale="en-US" />
          </div>
          <div class="field">
            <label>Notes (optional)</label>
            <input pInputText [(ngModel)]="newAccountNotes" />
          </div>
        </div>
        <ng-template #footer>
          <p-button label="Cancel" [text]="true" (onClick)="showAddDialog.set(false)" />
          <p-button label="Create" icon="pi pi-check" (onClick)="createAccount()" [loading]="saving()" />
        </ng-template>
      </p-dialog>
    </div>
  `,
  styles: [`
    .investments-root { display: flex; flex-direction: column; gap: 1rem; padding: 1rem; }
    .strip-numbers { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
    .strip-item { display: flex; flex-direction: column; }
    .strip-label { font-size: 0.75rem; color: var(--app-text-muted); text-transform: uppercase; letter-spacing: 0.05em; }
    .strip-value { font-size: 1.25rem; font-weight: 600; }
    .strip-sep { color: var(--app-text-muted); font-size: 1.25rem; }
    .strip-narrative { font-style: italic; color: var(--app-text-muted); margin-top: 0.5rem; font-size: 0.875rem; }
    .stale-warning { margin-top: 0.5rem; color: #f59e0b; font-size: 0.85rem; }
    .stale-warning a { cursor: pointer; text-decoration: underline; }
    .text-positive { color: #22c55e !important; }
    .text-negative { color: #ef4444 !important; }
    .text-muted { color: var(--app-text-muted); font-size: 0.875rem; }

    .invest-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    @media (max-width: 768px) { .invest-grid { grid-template-columns: 1fr; } }

    .widget-header { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; font-weight: 600; gap: 0.75rem; flex-wrap: wrap; }
    .alloc-toggle, .range-toggle { display: flex; gap: 0.25rem; flex-wrap: wrap; }
    .alloc-btn { background: none; border: 1px solid var(--app-text-muted); border-radius: 4px; padding: 0.2rem 0.5rem; font-size: 0.75rem; cursor: pointer; color: var(--app-text-color); }
    .alloc-btn.active { background: var(--p-primary-color); color: white; border-color: var(--p-primary-color); }

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

    .alloc-row { display: flex; align-items: center; gap: 0.5rem; padding: 0.5rem 1rem; }
    .alloc-label { flex: 1; font-size: 0.875rem; }
    .alloc-value { font-weight: 600; font-size: 0.875rem; min-width: 80px; text-align: right; }
    .alloc-pct { font-size: 0.75rem; color: var(--app-text-muted); min-width: 40px; text-align: right; }
    .alloc-bar-bg { width: 100px; height: 6px; background: var(--p-content-border-color); border-radius: 3px; overflow: hidden; }
    .alloc-bar { height: 100%; background: var(--p-primary-color); border-radius: 3px; }

    .portfolio-chart { width: 100%; height: 300px; }

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

    .manual-row { display: flex; align-items: center; gap: 0.75rem; padding: 0.6rem 1rem; border-bottom: 1px solid var(--p-content-border-color); flex-wrap: wrap; }
    .manual-balance { font-weight: 600; min-width: 80px; text-align: right; margin-left: auto; }

    .empty-state { padding: 2rem; text-align: center; color: var(--app-text-muted); font-style: italic; }
    .dialog-form { display: flex; flex-direction: column; gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.25rem; }
    .field label { font-size: 0.85rem; font-weight: 500; }
  `]
})
export class InvestmentsComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly accountTypes = ACCOUNT_TYPES;
  protected readonly allocationTypes = ALLOCATION_TYPES;
  protected readonly historyRanges = HISTORY_RANGES;

  protected readonly summary = signal<PortfolioSummary>({
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
  });
  protected readonly accounts = signal<AccountSummary[]>([]);
  protected readonly allocation = signal<AllocationBreakdown>({ allocationType: 'accountType', totalValue: 0, slices: [] });
  protected readonly history = signal<HistoryPoint[]>([]);
  protected readonly activity = signal<RecentActivity[]>([]);
  protected readonly manualAccounts = signal<ManualAccount[]>([]);
  protected readonly narrative = signal<NarrativeResponse | null>(null);

  protected readonly selectedAllocationType = signal('accountType');
  protected readonly selectedRange = signal('1Y');

  protected readonly showUpdateDialog = signal(false);
  protected readonly showAddDialog = signal(false);
  protected readonly showEditDialog = signal(false);
  protected readonly selectedAccount = signal<ManualAccount | null>(null);
  protected readonly saving = signal(false);
  protected newBalance: number | null = null;
  protected updateNote = '';
  protected newAccountName = '';
  protected newAccountType = 'Savings';
  protected newAccountCurrency = 'EUR';
  protected newAccountBalance: number | null = null;
  protected newAccountNotes = '';
  protected editAccountName = '';
  protected editAccountType = 'Savings';

  protected readonly chartOptions = computed<EChartsOption>(() => {
    const data = this.history();
    return {
      grid: { left: 60, right: 20, top: 20, bottom: 30 },
      xAxis: { type: 'category', data: data.map((point) => point.date), axisLabel: { fontSize: 11 } },
      yAxis: { type: 'value', axisLabel: { formatter: (value: number) => `€${(value / 1000).toFixed(0)}k` } },
      tooltip: {
        trigger: 'axis',
        formatter: (params: any) => {
          const point = Array.isArray(params) ? params[0] : params;
          return `${point.name}<br/>€${Number(point.value).toLocaleString('en', { maximumFractionDigits: 0 })}`;
        }
      },
      series: [{ type: 'line', data: data.map((point) => point.value), smooth: true, areaStyle: { opacity: 0.15 }, lineStyle: { width: 2 }, symbol: 'none' }]
    };
  });

  constructor() {
    this.loadData();
  }

  private loadData(): void {
    forkJoin({
      summary: this.api.getPortfolioSummary().pipe(catchError(() => of(this.summary()))),
      accounts: this.api.getInvestmentAccounts().pipe(catchError(() => of([]))),
      allocation: this.api.getInvestmentAllocation('accountType').pipe(catchError(() => of(this.allocation()))),
      history: this.api.getPortfolioHistory().pipe(catchError(() => of([]))),
      activity: this.api.getInvestmentActivity(15).pipe(catchError(() => of([]))),
      manualAccounts: this.api.getManualAccounts().pipe(catchError(() => of([]))),
      narrative: this.api.getInvestmentNarrative().pipe(catchError(() => of(null)))
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

  protected formatChange(amount: number | null, percent: number | null): string {
    if (amount === null) {
      return '—';
    }

    const sign = amount >= 0 ? '+' : '';
    const pct = percent !== null ? ` (${sign}${percent.toFixed(1)}%)` : '';
    return `${sign}€${Math.abs(amount).toLocaleString('en', { maximumFractionDigits: 0 })}${pct}`;
  }

  protected daysSince(dateStr: string | null): number {
    if (!dateStr) {
      return 999;
    }

    return Math.floor((Date.now() - new Date(dateStr).getTime()) / 86400000);
  }

  protected changeAllocationType(type: string): void {
    this.selectedAllocationType.set(type);
    this.api.getInvestmentAllocation(type)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((allocation) => this.allocation.set(allocation));
  }

  protected changeHistoryRange(range: { label: string; months: number }): void {
    this.selectedRange.set(range.label);
    const now = new Date();
    let from: string | undefined;

    if (range.months === 0) {
      from = `${now.getFullYear()}-01-01`;
    } else if (range.months > 0) {
      const date = new Date(now);
      date.setMonth(date.getMonth() - range.months);
      from = date.toISOString().split('T')[0];
    }

    this.api.getPortfolioHistory(from)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((history) => this.history.set(history));
  }

  protected onAccountClick(account: AccountSummary): void {
    if (account.providerType === 'manual') {
      const manual = this.manualAccounts().find((item) => item.id === account.accountId);
      if (manual) {
        this.openUpdateBalanceDialog(manual);
      }
    }
  }

  protected scrollToManual(): void {
    document.getElementById('manual-accounts')?.scrollIntoView({ behavior: 'smooth' });
  }

  protected openUpdateBalanceDialog(account: ManualAccount): void {
    this.selectedAccount.set(account);
    this.newBalance = account.balance;
    this.updateNote = '';
    this.showUpdateDialog.set(true);
  }

  protected saveBalance(): void {
    const account = this.selectedAccount();
    if (!account || this.newBalance === null) {
      return;
    }

    this.saving.set(true);
    this.api.updateManualBalance(account.id, { newBalance: this.newBalance, note: this.updateNote || undefined })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.showUpdateDialog.set(false);
          this.saving.set(false);
          this.loadData();
        },
        error: () => this.saving.set(false)
      });
  }

  protected openAddAccountDialog(): void {
    this.newAccountName = '';
    this.newAccountType = 'Savings';
    this.newAccountCurrency = 'EUR';
    this.newAccountBalance = null;
    this.newAccountNotes = '';
    this.showAddDialog.set(true);
  }

  protected createAccount(): void {
    if (!this.newAccountName) {
      return;
    }

    this.saving.set(true);
    this.api.createManualAccount({
      displayName: this.newAccountName,
      accountType: this.newAccountType,
      currency: this.newAccountCurrency,
      initialBalance: this.newAccountBalance ?? undefined,
      notes: this.newAccountNotes || undefined
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showAddDialog.set(false);
        this.saving.set(false);
        this.loadData();
      },
      error: () => this.saving.set(false)
    });
  }

  protected openEditAccountDialog(account: ManualAccount): void {
    this.selectedAccount.set(account);
    this.editAccountName = account.displayName;
    this.editAccountType = account.accountType;
    this.showEditDialog.set(true);
  }

  protected saveEditAccount(): void {
    const account = this.selectedAccount();
    if (!account || !this.editAccountName) return;

    this.saving.set(true);
    this.api.updateManualAccount(account.id, {
      displayName: this.editAccountName,
      accountType: this.editAccountType
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showEditDialog.set(false);
        this.saving.set(false);
        this.loadData();
      },
      error: () => this.saving.set(false)
    });
  }

  protected confirmDeleteAccount(account: ManualAccount): void {
    this.confirmationService.confirm({
      message: `Delete "${account.displayName}"? This will remove all its balance history.`,
      header: 'Delete account',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete',
      rejectLabel: 'Cancel',
      accept: () => {
        this.api.deleteManualAccount(account.id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({ next: () => this.loadData() });
      }
    });
  }
}

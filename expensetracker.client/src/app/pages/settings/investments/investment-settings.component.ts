import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AccordionModule } from 'primeng/accordion';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { ApiService, InvestmentProvider } from '../../../core/services/api.service';

@Component({
  standalone: true,
  selector: 'app-investment-settings',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    CardModule,
    ButtonModule,
    InputTextModule,
    ToggleSwitchModule,
    TagModule,
    AccordionModule,
    MessageModule
  ],
  template: `
    <div class="settings-root">
      <h2>Investment providers</h2>

      @if (ibkrProvider(); as ibkr) {
        <p-card>
          <ng-template #header>
            <div class="provider-header">
              <span class="provider-title">
                <i class="pi pi-chart-line"></i> Interactive Brokers
              </span>
              <p-tag [severity]="ibkr.isEnabled ? 'success' : 'warn'" [value]="ibkr.isEnabled ? 'Enabled' : 'Disabled'" />
            </div>
          </ng-template>

          <div class="provider-body">
            @if (tokenExpiryDays() !== null && tokenExpiryDays()! <= 14) {
              <p-message [severity]="tokenExpiryDays()! <= 0 ? 'error' : 'warn'"
                [text]="tokenExpiryDays()! <= 0 ? 'IBKR Flex token has expired! Sync will fail until you generate a new token.' : 'IBKR Flex token expires in ' + tokenExpiryDays() + ' day(s). Generate a new token soon.'" />
            }
            @if (ibkr.lastSyncAt) {
              <div class="status-line">
                Last sync: {{ ibkr.lastSyncAt | date:'medium' }}
                <p-tag [severity]="ibkr.lastSyncStatus === 'success' ? 'success' : 'danger'" [value]="ibkr.lastSyncStatus ?? 'never'" />
              </div>
            }
            @if (ibkr.lastSyncError) {
              <p-message severity="error" [text]="ibkr.lastSyncError" />
            }

            <div class="form-grid">
              <div class="field">
                <label>Flex Token</label>
                <input pInputText [(ngModel)]="ibkrToken" placeholder="Enter IBKR Flex token" class="w-full" />
              </div>
              <div class="field">
                <label>Token expires</label>
                <input pInputText [(ngModel)]="ibkrTokenExpiry" placeholder="YYYY-MM-DD" />
              </div>
              <div class="field">
                <label>Positions Query ID</label>
                <input pInputText [(ngModel)]="ibkrPositionsQuery" placeholder="e.g., 1234567" />
              </div>
              <div class="field">
                <label>Trades Query ID</label>
                <input pInputText [(ngModel)]="ibkrTradesQuery" />
              </div>
              <div class="field">
                <label>Cash Report Query ID</label>
                <input pInputText [(ngModel)]="ibkrCashQuery" />
              </div>
              <div class="field">
                <label>NAV Query ID (optional)</label>
                <input pInputText [(ngModel)]="ibkrNavQuery" />
              </div>
            </div>

            <div class="button-row">
              <p-button label="Save configuration" icon="pi pi-save" (onClick)="saveIbkrConfig()" [loading]="savingIbkr()" />
              <p-button label="Test connection" icon="pi pi-play" severity="secondary" (onClick)="testIbkr()" [loading]="testingIbkr()" />
              @if (!ibkr.isEnabled) {
                <p-button label="Enable" icon="pi pi-check" severity="success" [outlined]="true" (onClick)="toggleIbkr(true)" />
              } @else {
                <p-button label="Disable" icon="pi pi-times" severity="danger" [outlined]="true" (onClick)="toggleIbkr(false)" />
              }
              @if (ibkr.isEnabled) {
                <p-button label="Sync now" icon="pi pi-sync" severity="info" [outlined]="true" (onClick)="syncNow()" [loading]="syncing()" />
              }
            </div>

            @if (testResult()) {
              <div class="test-result" [class.success]="testResult()!.success" [class.failure]="!testResult()!.success">
                <i [class]="testResult()!.success ? 'pi pi-check-circle' : 'pi pi-times-circle'"></i>
                {{ testResult()!.message }} ({{ testResult()!.latencyMs }}ms)
              </div>
            }

            <p-accordion>
              <p-accordion-panel value="setup-guide">
                <p-accordion-header>How to set up IBKR Flex Web Service</p-accordion-header>
                <p-accordion-content>
                  <ol class="setup-steps">
                    <li>Log into IBKR Client Portal</li>
                    <li>Settings → Account Settings → Reporting → Flex Web Service</li>
                    <li>Enable Flex Web Service, generate token (shown once — save it)</li>
                    <li>Settings → Account Settings → Reporting → Flex Queries</li>
                    <li>Create three Activity Flex Queries:
                      <ul>
                        <li>"Positions": Sections = Open Positions, all fields</li>
                        <li>"Trades": Sections = Trades, all fields</li>
                        <li>"Cash Report": Sections = Cash Report, all fields</li>
                        <li>(Optional) "NAV": Sections = Net Asset Value, all fields</li>
                      </ul>
                    </li>
                    <li>For each, set Date Range = "Last Business Day"</li>
                    <li>Save each query, copy the Query ID from the URL</li>
                    <li>Enter token + Query IDs in fields above</li>
                    <li>"Test connection", then enable</li>
                  </ol>
                  <p class="text-muted">Sync runs daily at 23:00 UTC. Trigger manually with "Sync now".</p>
                </p-accordion-content>
              </p-accordion-panel>
            </p-accordion>
          </div>
        </p-card>
      }

      @if (manualProvider(); as manual) {
        <p-card>
          <ng-template #header>
            <div class="provider-header">
              <span class="provider-title">
                <i class="pi pi-pencil"></i> Manual entries
              </span>
              <p-tag severity="success" value="Always available" />
            </div>
          </ng-template>
          <div class="provider-body">
            <p>Track accounts that aren't connected via API — savings accounts, crypto, cash, etc. You enter the balance manually and update it whenever you like.</p>
            <div class="status-line">
              {{ manualAccountCount() }} manual account(s) · Total value {{ manualTotal() | currency:'EUR':'symbol':'1.0-0' }}
              @if (oldestManualUpdate() !== null) {
                <span>· Oldest update: {{ oldestManualUpdate() }}d ago</span>
              }
            </div>
            <p-button label="Manage manual accounts →" [text]="true" icon="pi pi-arrow-right" routerLink="/investments" />
          </div>
        </p-card>
      }
    </div>
  `,
  styles: [`
    .settings-root { display: flex; flex-direction: column; gap: 1rem; padding: 1rem; max-width: 900px; }
    h2 { margin: 0 0 0.5rem 0; }
    .provider-header { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; gap: 0.75rem; }
    .provider-title { display: flex; align-items: center; gap: 0.5rem; font-weight: 600; font-size: 1.1rem; }
    .provider-body { display: flex; flex-direction: column; gap: 1rem; }
    .status-line { font-size: 0.85rem; color: var(--app-text-muted); display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    @media (max-width: 600px) { .form-grid { grid-template-columns: 1fr; } }
    .field { display: flex; flex-direction: column; gap: 0.25rem; }
    .field label { font-size: 0.85rem; font-weight: 500; }
    .button-row { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .test-result { padding: 0.75rem; border-radius: 6px; display: flex; align-items: center; gap: 0.5rem; }
    .test-result.success { background: #dcfce7; color: #166534; }
    .test-result.failure { background: #fee2e2; color: #991b1b; }
    .setup-steps { padding-left: 1.5rem; line-height: 1.8; }
    .setup-steps ul { padding-left: 1.5rem; margin: 0.25rem 0; }
    .text-muted { color: var(--app-text-muted); }
  `]
})
export class InvestmentSettingsComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly ibkrProvider = signal<InvestmentProvider | null>(null);
  protected readonly manualProvider = signal<InvestmentProvider | null>(null);
  protected readonly manualAccountCount = signal(0);
  protected readonly manualTotal = signal(0);
  protected readonly oldestManualUpdate = signal<number | null>(null);

  protected readonly tokenExpiryDays = computed<number | null>(() => {
    const ibkr = this.ibkrProvider();
    const expiry = ibkr?.extraConfig?.tokenExpiresAt;
    if (!expiry) return null;
    const days = Math.floor((new Date(expiry).getTime() - Date.now()) / 86400000);
    return days;
  });

  protected ibkrToken = '';
  protected ibkrTokenExpiry = '';
  protected ibkrPositionsQuery = '';
  protected ibkrTradesQuery = '';
  protected ibkrCashQuery = '';
  protected ibkrNavQuery = '';

  protected readonly savingIbkr = signal(false);
  protected readonly testingIbkr = signal(false);
  protected readonly syncing = signal(false);
  protected readonly testResult = signal<{ success: boolean; message: string; latencyMs: number } | null>(null);

  constructor() {
    this.loadProviders();
  }

  private loadProviders(): void {
    forkJoin({
      providers: this.api.getInvestmentProviders().pipe(catchError(() => of<InvestmentProvider[]>([]))),
      manualAccounts: this.api.getManualAccounts().pipe(catchError(() => of([])))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      const ibkr = data.providers.find((provider) => provider.providerType === 'ibkr');
      const manual = data.providers.find((provider) => provider.providerType === 'manual');
      this.ibkrProvider.set(ibkr ?? null);
      this.manualProvider.set(manual ?? null);

      if (ibkr?.extraConfig) {
        this.ibkrPositionsQuery = ibkr.extraConfig.positionsQueryId ?? '';
        this.ibkrTradesQuery = ibkr.extraConfig.tradesQueryId ?? '';
        this.ibkrCashQuery = ibkr.extraConfig.cashQueryId ?? '';
        this.ibkrNavQuery = ibkr.extraConfig.navQueryId ?? '';
        this.ibkrTokenExpiry = ibkr.extraConfig.tokenExpiresAt ?? '';
      }
      if (ibkr?.token) {
        this.ibkrToken = ibkr.token;
      }

      this.manualAccountCount.set(data.manualAccounts.length);
      this.manualTotal.set(data.manualAccounts.reduce((sum, account) => sum + (account.balance ?? 0), 0));
      const oldest = data.manualAccounts
        .filter((account) => account.lastUpdated)
        .map((account) => Math.floor((Date.now() - new Date(account.lastUpdated!).getTime()) / 86400000))
        .sort((a, b) => b - a)[0];
      this.oldestManualUpdate.set(oldest ?? null);
    });
  }

  protected saveIbkrConfig(): void {
    const ibkr = this.ibkrProvider();
    if (!ibkr) {
      return;
    }

    this.savingIbkr.set(true);
    const payload: { apiToken?: string; extraConfig: Record<string, string | null> } = {
      extraConfig: {
        positionsQueryId: this.ibkrPositionsQuery,
        tradesQueryId: this.ibkrTradesQuery,
        cashQueryId: this.ibkrCashQuery,
        navQueryId: this.ibkrNavQuery || null,
        tokenExpiresAt: this.ibkrTokenExpiry || null
      }
    };

    if (this.ibkrToken) {
      payload.apiToken = this.ibkrToken;
    }

    this.api.updateInvestmentProvider(ibkr.id, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingIbkr.set(false);
          this.loadProviders();
        },
        error: () => this.savingIbkr.set(false)
      });
  }

  protected testIbkr(): void {
    const ibkr = this.ibkrProvider();
    if (!ibkr) {
      return;
    }

    this.testingIbkr.set(true);
    this.testResult.set(null);
    this.api.testInvestmentProvider(ibkr.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.testResult.set(result);
          this.testingIbkr.set(false);
          this.loadProviders();
        },
        error: (error) => {
          this.testResult.set({ success: false, message: error.error?.message ?? 'Test failed', latencyMs: 0 });
          this.testingIbkr.set(false);
        }
      });
  }

  protected toggleIbkr(enabled: boolean): void {
    const ibkr = this.ibkrProvider();
    if (!ibkr) {
      return;
    }

    const operation = enabled ? this.api.enableInvestmentProvider(ibkr.id) : this.api.disableInvestmentProvider(ibkr.id);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.loadProviders());
  }

  protected syncNow(): void {
    this.syncing.set(true);
    this.api.triggerInvestmentSync()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.syncing.set(false);
          this.loadProviders();
        },
        error: () => this.syncing.set(false)
      });
  }
}

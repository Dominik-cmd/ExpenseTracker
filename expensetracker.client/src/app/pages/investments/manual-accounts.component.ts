import { CommonModule } from '@angular/common';
import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';

import { ManualAccount } from '../../core/models';
import { AppCurrencyPipe } from '../../shared';

const ACCOUNT_TYPES = [
  { label: 'Broker', value: 'Broker' },
  { label: 'Savings', value: 'Savings' },
  { label: 'Crypto', value: 'Crypto' },
  { label: 'Cash', value: 'Cash' },
  { label: 'Pension', value: 'Pension' },
  { label: 'Real Estate', value: 'RealEstate' },
  { label: 'Other', value: 'Other' }
];

interface ManualAccountActionCallbacks {
  succeed: () => void;
  fail: () => void;
}

export interface CreateManualAccountRequest extends ManualAccountActionCallbacks {
  payload: {
    displayName: string;
    accountType: string;
    currency?: string;
    initialBalance?: number;
    notes?: string;
  };
}

export interface UpdateManualAccountRequest extends ManualAccountActionCallbacks {
  accountId: string;
  payload: {
    displayName: string;
    accountType: string;
  };
}

export interface DeleteManualAccountRequest extends ManualAccountActionCallbacks {
  accountId: string;
}

export interface BalanceUpdateRequest extends ManualAccountActionCallbacks {
  accountId: string;
  payload: {
    newBalance: number;
    note?: string;
  };
}

@Component({
  selector: 'app-manual-accounts',
  standalone: true,
  imports: [
    CommonModule,
    AppCurrencyPipe,
    FormsModule,
    ButtonModule,
    CardModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    SelectModule
  ],
  template: `
    <p-card>
      <ng-template #header>
        <div class="widget-header">
          <span>Manual accounts</span>
          <p-button label="Add account" icon="pi pi-plus" [text]="true" (onClick)="openAddAccountDialog()" />
        </div>
      </ng-template>

      @for (account of accounts(); track account.id) {
        <div class="manual-row">
          <span class="account-dot" [style.background]="account.color ?? '#607D8B'"></span>
          <span class="account-info">
            <span class="account-name">{{ account.displayName }}</span>
            <span class="account-sub">{{ account.accountType }}</span>
          </span>
          <span class="manual-balance">{{ (account.balance ?? 0) | appCurrency:'1.0-0' }}</span>
          @if (account.lastUpdated) {
            <span class="account-updated" [class.stale]="daysSince(account.lastUpdated) > 30">
              Updated {{ daysSince(account.lastUpdated) }}d ago
              @if (daysSince(account.lastUpdated) > 30) { <i class="pi pi-exclamation-triangle"></i> }
            </span>
          }
          <p-button label="Balance" icon="pi pi-wallet" [text]="true" size="small" (onClick)="openBalanceDialog(account)" />
          <p-button icon="pi pi-pencil" [text]="true" size="small" severity="secondary" (onClick)="openEditAccountDialog(account)" />
          <p-button icon="pi pi-trash" [text]="true" size="small" severity="danger" (onClick)="openDeleteAccountDialog(account)" />
        </div>
      }

      @if (accounts().length === 0) {
        <div class="empty-state">No manual accounts yet. Click "Add account" to start tracking.</div>
      }
    </p-card>

    <p-dialog
      [header]="'Update ' + (selectedAccount()?.displayName ?? '')"
      [visible]="showBalanceDialog()"
      (visibleChange)="showBalanceDialog.set($event)"
      [modal]="true"
      [style]="{ width: '400px' }">
      <div class="dialog-form">
        <label>Current balance: {{ (selectedAccount()?.balance ?? 0) | appCurrency:'1.0-0' }}</label>
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
        <p-button label="Cancel" [text]="true" (onClick)="showBalanceDialog.set(false)" [disabled]="saving()" />
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
        <p-button label="Cancel" [text]="true" (onClick)="showEditDialog.set(false)" [disabled]="saving()" />
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
        <p-button label="Cancel" [text]="true" (onClick)="showAddDialog.set(false)" [disabled]="saving()" />
        <p-button label="Create" icon="pi pi-check" (onClick)="createAccount()" [loading]="saving()" />
      </ng-template>
    </p-dialog>

    <p-dialog
      header="Delete account"
      [visible]="showDeleteDialog()"
      (visibleChange)="showDeleteDialog.set($event)"
      [modal]="true"
      [style]="{ width: '400px' }">
      <p>Delete "{{ accountPendingDelete()?.displayName ?? '' }}"? This will remove all its balance history.</p>
      <ng-template #footer>
        <p-button label="Cancel" [text]="true" (onClick)="showDeleteDialog.set(false)" [disabled]="saving()" />
        <p-button label="Delete" icon="pi pi-trash" severity="danger" (onClick)="confirmDeleteAccount()" [loading]="saving()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .widget-header { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; font-weight: 600; gap: 0.75rem; flex-wrap: wrap; }
    .manual-row { display: flex; align-items: center; gap: 0.75rem; padding: 0.6rem 1rem; border-bottom: 1px solid var(--p-content-border-color); flex-wrap: wrap; }
    .account-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
    .account-info { display: flex; flex-direction: column; flex: 1; min-width: 0; }
    .account-name { font-weight: 600; font-size: 0.9rem; }
    .account-sub { font-size: 0.75rem; color: var(--app-text-muted); }
    .manual-balance { font-weight: 600; min-width: 80px; text-align: right; margin-left: auto; }
    .account-updated { font-size: 0.7rem; color: var(--app-text-muted); }
    .account-updated.stale { color: #f59e0b; }
    .empty-state { padding: 2rem; text-align: center; color: var(--app-text-muted); font-style: italic; }
    .dialog-form { display: flex; flex-direction: column; gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.25rem; }
    .field label { font-size: 0.85rem; font-weight: 500; }
  `]
})
export class ManualAccountsComponent {
  readonly accounts = input<ManualAccount[]>([]);
  readonly accountCreated = output<CreateManualAccountRequest>();
  readonly accountUpdated = output<UpdateManualAccountRequest>();
  readonly accountDeleted = output<DeleteManualAccountRequest>();
  readonly balanceUpdated = output<BalanceUpdateRequest>();

  protected readonly accountTypes = ACCOUNT_TYPES;
  protected readonly showBalanceDialog = signal(false);
  protected readonly showAddDialog = signal(false);
  protected readonly showEditDialog = signal(false);
  protected readonly showDeleteDialog = signal(false);
  protected readonly selectedAccount = signal<ManualAccount | null>(null);
  protected readonly accountPendingDelete = signal<ManualAccount | null>(null);
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

  public openBalanceDialog(account: ManualAccount): void {
    this.selectedAccount.set(account);
    this.newBalance = account.balance;
    this.updateNote = '';
    this.showBalanceDialog.set(true);
  }

  protected daysSince(dateStr: string | null): number {
    if (!dateStr) {
      return 999;
    }

    return Math.floor((Date.now() - new Date(dateStr).getTime()) / 86400000);
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
    const displayName = this.newAccountName.trim();
    if (!displayName) {
      return;
    }

    this.saving.set(true);
    this.accountCreated.emit({
      payload: {
        displayName,
        accountType: this.newAccountType,
        currency: this.newAccountCurrency.trim() || undefined,
        initialBalance: this.newAccountBalance ?? undefined,
        notes: this.newAccountNotes.trim() || undefined
      },
      succeed: () => {
        this.showAddDialog.set(false);
        this.saving.set(false);
      },
      fail: () => this.saving.set(false)
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
    const displayName = this.editAccountName.trim();
    if (!account || !displayName) {
      return;
    }

    this.saving.set(true);
    this.accountUpdated.emit({
      accountId: account.id,
      payload: {
        displayName,
        accountType: this.editAccountType
      },
      succeed: () => {
        this.showEditDialog.set(false);
        this.saving.set(false);
      },
      fail: () => this.saving.set(false)
    });
  }

  protected openDeleteAccountDialog(account: ManualAccount): void {
    this.accountPendingDelete.set(account);
    this.showDeleteDialog.set(true);
  }

  protected confirmDeleteAccount(): void {
    const account = this.accountPendingDelete();
    if (!account) {
      return;
    }

    this.saving.set(true);
    this.accountDeleted.emit({
      accountId: account.id,
      succeed: () => {
        this.showDeleteDialog.set(false);
        this.saving.set(false);
      },
      fail: () => this.saving.set(false)
    });
  }

  protected saveBalance(): void {
    const account = this.selectedAccount();
    if (!account || this.newBalance === null) {
      return;
    }

    this.saving.set(true);
    this.balanceUpdated.emit({
      accountId: account.id,
      payload: {
        newBalance: this.newBalance,
        note: this.updateNote.trim() || undefined
      },
      succeed: () => {
        this.showBalanceDialog.set(false);
        this.saving.set(false);
      },
      fail: () => this.saving.set(false)
    });
  }
}

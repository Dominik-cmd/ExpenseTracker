import { Component, effect, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';

import { Transaction } from '../../core/models';

interface TransactionOption {
  label: string;
  value: string;
}

export interface TransactionDialogSaveEvent {
  amount: number | null;
  currency: string;
  direction: string;
  transactionType: string;
  transactionDate: string;
  merchantRaw: string;
  categoryId: string;
  notes: string;
}

@Component({
  standalone: true,
  selector: 'app-transaction-dialog',
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule],
  template: `
    <p-dialog
      [visible]="visible()"
      [modal]="true"
      [style]="{ width: 'min(38rem, 95vw)' }"
      [header]="editingTransaction() ? 'Edit transaction' : 'Manual transaction entry'"
      (visibleChange)="handleVisibleChange($event)">
      <div class="p-fluid grid mt-1">
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnAmount">Amount</label>
          <input id="txnAmount" type="number" step="0.01" pInputText [(ngModel)]="form.amount" />
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnDate">Transaction date</label>
          <input id="txnDate" type="date" pInputText [(ngModel)]="form.transactionDate" />
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnDirection">Direction</label>
          <select id="txnDirection" class="native-select" [(ngModel)]="form.direction">
            @for (option of directionOptions(); track option) {
              <option [value]="option">{{ option }}</option>
            }
          </select>
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnType">Type</label>
          <select id="txnType" class="native-select" [(ngModel)]="form.transactionType">
            @for (option of transactionTypeOptions(); track option) {
              <option [value]="option">{{ option }}</option>
            }
          </select>
        </div>
        <div class="col-12">
          <label class="field-label" for="txnMerchant">Merchant</label>
          <input id="txnMerchant" type="text" pInputText [(ngModel)]="form.merchantRaw" placeholder="e.g. MERCATOR" />
        </div>
        <div class="col-12">
          <label class="field-label" for="txnCategory">Category</label>
          <select id="txnCategory" class="native-select" [(ngModel)]="form.categoryId">
            @for (option of categories(); track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
        </div>
        <div class="col-12">
          <label class="field-label" for="txnNotes">Notes</label>
          <input id="txnNotes" type="text" pInputText [(ngModel)]="form.notes" placeholder="Optional notes" />
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Cancel" severity="secondary" [outlined]="true" (onClick)="cancel.emit()"></p-button>
          <p-button label="Save transaction" icon="pi pi-save" [loading]="saving()" (onClick)="submit()"></p-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .field-label {
      display: block;
      font-size: 0.875rem;
      color: var(--text-color-secondary);
      margin-bottom: 0.35rem;
    }

    .native-select {
      width: 100%;
      padding: 0.75rem 0.875rem;
      border: 1px solid var(--surface-border);
      border-radius: var(--border-radius);
      background: var(--surface-card);
      color: inherit;
    }
  `]
})
export class TransactionDialogComponent {
  readonly visible = input(false);
  readonly editingTransaction = input<Transaction | null>(null);
  readonly categories = input<TransactionOption[]>([]);
  readonly directionOptions = input<string[]>([]);
  readonly transactionTypeOptions = input<string[]>([]);
  readonly saving = input(false);
  readonly save = output<TransactionDialogSaveEvent>();
  readonly cancel = output<void>();

  protected form: TransactionDialogSaveEvent = this.createEmptyForm();
  private resetKey: string | null = null;

  constructor() {
    effect(() => {
      const visible = this.visible();
      const editingTransaction = this.editingTransaction();
      const categories = this.categories();

      if (!visible) {
        this.resetKey = null;
        return;
      }

      const key = `${editingTransaction?.id ?? 'create'}:${categories[0]?.value ?? ''}`;
      if (this.resetKey === key) {
        return;
      }

      this.form = editingTransaction
        ? this.mapTransactionToForm(editingTransaction)
        : this.createEmptyForm(categories[0]?.value ?? '');
      this.resetKey = key;
    });
  }

  protected submit(): void {
    this.save.emit({ ...this.form });
  }

  protected handleVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancel.emit();
    }
  }

  private createEmptyForm(categoryId = ''): TransactionDialogSaveEvent {
    return {
      amount: null,
      currency: 'EUR',
      direction: 'Debit',
      transactionType: 'Purchase',
      transactionDate: this.toDateInputValue(new Date().toISOString()),
      merchantRaw: '',
      categoryId,
      notes: ''
    };
  }

  private mapTransactionToForm(transaction: Transaction): TransactionDialogSaveEvent {
    return {
      amount: transaction.amount,
      currency: transaction.currency,
      direction: transaction.direction,
      transactionType: transaction.transactionType,
      transactionDate: this.toDateInputValue(transaction.transactionDate),
      merchantRaw: transaction.merchantRaw ?? transaction.merchantNormalized ?? '',
      categoryId: transaction.categoryId,
      notes: transaction.notes ?? ''
    };
  }

  private toDateInputValue(value: string): string {
    return value.slice(0, 10);
  }
}

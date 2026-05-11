import { Component, effect, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';

import { Transaction } from '../../core/models';

interface TransactionOption {
  label: string;
  value: string;
}

export interface TransactionRecategorizeEvent {
  categoryId: string;
  createMerchantRule: boolean;
}

@Component({
  standalone: true,
  selector: 'app-transaction-recategorize',
  imports: [FormsModule, ButtonModule, CheckboxModule, DialogModule],
  template: `
    <p-dialog
      [visible]="visible()"
      [modal]="true"
      [style]="{ width: 'min(28rem, 95vw)' }"
      [header]="transaction() ? 'Recategorize transaction' : 'Bulk recategorize transactions'"
      (visibleChange)="handleVisibleChange($event)">
      <div class="p-fluid flex flex-column gap-3 mt-1">
        <div>
          <label class="field-label" for="recategorizeCategory">New category</label>
          <select id="recategorizeCategory" class="native-select" [(ngModel)]="selectedCategoryId">
            <option value="" disabled>Select a category</option>
            @for (option of categories(); track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
        </div>
        <div class="flex align-items-center gap-2">
          <p-checkbox inputId="createRule" [(ngModel)]="createMerchantRule" [binary]="true"></p-checkbox>
          <label for="createRule">Create or refresh a merchant rule while recategorizing</label>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Cancel" severity="secondary" [outlined]="true" (onClick)="cancel.emit()"></p-button>
          <p-button label="Apply category" icon="pi pi-check" [loading]="saving()" (onClick)="submit()"></p-button>
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
export class TransactionRecategorizeComponent {
  readonly visible = input(false);
  readonly transaction = input<Transaction | null>(null);
  readonly categories = input<TransactionOption[]>([]);
  readonly initialCategoryId = input('');
  readonly saving = input(false);
  readonly recategorize = output<TransactionRecategorizeEvent>();
  readonly cancel = output<void>();

  protected selectedCategoryId = '';
  protected createMerchantRule = true;
  private resetKey: string | null = null;

  constructor() {
    effect(() => {
      const visible = this.visible();
      const transaction = this.transaction();
      const initialCategoryId = this.initialCategoryId();
      const categories = this.categories();

      if (!visible) {
        this.resetKey = null;
        return;
      }

      const key = `${transaction?.id ?? 'bulk'}:${initialCategoryId}:${categories[0]?.value ?? ''}`;
      if (this.resetKey === key) {
        return;
      }

      this.selectedCategoryId = transaction?.categoryId || initialCategoryId || categories[0]?.value || '';
      this.createMerchantRule = true;
      this.resetKey = key;
    });
  }

  protected submit(): void {
    this.recategorize.emit({
      categoryId: this.selectedCategoryId,
      createMerchantRule: this.createMerchantRule
    });
  }

  protected handleVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancel.emit();
    }
  }
}

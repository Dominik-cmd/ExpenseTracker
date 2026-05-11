import { Component, computed, effect, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';

interface TransactionOption {
  label: string;
  value: string;
}

export interface TransactionFilterState {
  from: string;
  to: string;
  categoryIds: string[];
  merchant: string;
  minAmount: number | null;
  maxAmount: number | null;
  direction: string;
}

@Component({
  standalone: true,
  selector: 'app-transaction-filters',
  imports: [FormsModule, ButtonModule, InputTextModule, MultiSelectModule],
  template: `
    <div class="p-fluid flex flex-column gap-3 mt-2">
      <div class="grid">
        <div class="col-6">
          <label class="field-label" for="fromDate">From</label>
          <input id="fromDate" type="date" pInputText [(ngModel)]="draft.from" />
        </div>
        <div class="col-6">
          <label class="field-label" for="toDate">To</label>
          <input id="toDate" type="date" pInputText [(ngModel)]="draft.to" />
        </div>
      </div>

      <div>
        <label class="field-label" for="categoryFilter">Categories</label>
        <p-multiSelect
          inputId="categoryFilter"
          [options]="categoryOptions()"
          optionLabel="label"
          optionValue="value"
          [filter]="true"
          defaultLabel="All categories"
          [(ngModel)]="draft.categoryIds"></p-multiSelect>
      </div>

      <div>
        <label class="field-label" for="merchantFilter">Merchant</label>
        <input id="merchantFilter" type="text" pInputText [(ngModel)]="draft.merchant" placeholder="Search merchant" />
      </div>

      <div class="grid">
        <div class="col-6">
          <label class="field-label" for="minAmount">Min amount</label>
          <input id="minAmount" type="number" step="0.01" pInputText [(ngModel)]="draft.minAmount" />
        </div>
        <div class="col-6">
          <label class="field-label" for="maxAmount">Max amount</label>
          <input id="maxAmount" type="number" step="0.01" pInputText [(ngModel)]="draft.maxAmount" />
        </div>
      </div>

      <div>
        <label class="field-label" for="directionFilter">Direction</label>
        <select id="directionFilter" class="native-select" [(ngModel)]="draft.direction">
          <option value="">All directions</option>
          @for (option of directionOptions(); track option) {
            <option [value]="option">{{ option }}</option>
          }
        </select>
      </div>

      <div class="flex flex-wrap justify-content-end gap-2 pt-2">
        <p-button label="Reset" severity="secondary" [outlined]="true" (onClick)="clear()"></p-button>
        <p-button label="Apply filters" icon="pi pi-check" (onClick)="apply()"></p-button>
      </div>
    </div>
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
export class TransactionFiltersComponent {
  readonly filterState = input.required<TransactionFilterState>();
  readonly categories = input.required<TransactionOption[]>();
  readonly directionOptions = input.required<string[]>();
  readonly applyFilters = output<TransactionFilterState>();
  readonly clearFilters = output<void>();

  protected readonly categoryOptions = computed(() => [...this.categories()]);

  protected draft: TransactionFilterState = this.createEmptyFilters();

  constructor() {
    effect(() => {
      this.draft = this.cloneState(this.filterState());
    });
  }

  protected apply(): void {
    this.applyFilters.emit(this.cloneState(this.draft));
  }

  protected clear(): void {
    this.draft = this.createEmptyFilters();
    this.clearFilters.emit();
  }

  private cloneState(state: TransactionFilterState): TransactionFilterState {
    return {
      ...state,
      categoryIds: [...state.categoryIds]
    };
  }

  private createEmptyFilters(): TransactionFilterState {
    return {
      from: '',
      to: '',
      categoryIds: [],
      merchant: '',
      minAmount: null,
      maxAmount: null,
      direction: ''
    };
  }
}

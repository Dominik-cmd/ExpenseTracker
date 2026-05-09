import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, map, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SidebarModule } from 'primeng/sidebar';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import {
  ApiService,
  Category,
  CreateTransactionRequest,
  PagedResult,
  RecategorizeTransactionRequest,
  Transaction,
  TransactionFilters,
  UpdateTransactionRequest
} from '../../core/services/api.service';

interface TransactionFilterState {
  from: string;
  to: string;
  categoryIds: string[];
  merchant: string;
  minAmount: string;
  maxAmount: string;
  direction: string;
}

interface TransactionFormModel {
  amount: number | null;
  currency: string;
  direction: string;
  transactionType: string;
  transactionDate: string;
  merchantRaw: string;
  categoryId: string;
  notes: string;
}

const TRANSACTIONS_FALLBACK: PagedResult<Transaction> = {
  items: [
    {
      id: '1', userId: 'demo', amount: 52.4, currency: 'EUR', direction: 'Debit', transactionType: 'Purchase', transactionDate: new Date().toISOString(),
      merchantRaw: 'MERCATOR', merchantNormalized: 'MERCATOR', categoryId: 'groceries', categorySource: 'Rule', transactionSource: 'Sms', notes: 'Weekly groceries',
      isDeleted: false, rawMessageId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), categoryName: 'Groceries', parentCategoryName: null
    },
    {
      id: '2', userId: 'demo', amount: 1200, currency: 'EUR', direction: 'Credit', transactionType: 'Transfer', transactionDate: new Date(Date.now() - 86400000 * 3).toISOString(),
      merchantRaw: 'Salary', merchantNormalized: 'EMPLOYER', categoryId: 'income', categorySource: 'Manual', transactionSource: 'Manual', notes: 'Monthly salary',
      isDeleted: false, rawMessageId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), categoryName: 'Income', parentCategoryName: null
    },
    {
      id: '3', userId: 'demo', amount: 61.1, currency: 'EUR', direction: 'Debit', transactionType: 'Purchase', transactionDate: new Date(Date.now() - 86400000).toISOString(),
      merchantRaw: 'OMV', merchantNormalized: 'OMV', categoryId: 'fuel', categorySource: 'Llm', transactionSource: 'Sms', notes: 'Fuel top-up',
      isDeleted: false, rawMessageId: 'raw-3', createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), categoryName: 'Fuel', parentCategoryName: null
    }
  ],
  totalCount: 3,
  page: 1,
  pageSize: 10
};

const CATEGORIES_FALLBACK: Category[] = [
  {
    id: 'groceries', name: 'Groceries', color: '#10b981', icon: 'pi pi-shopping-basket', sortOrder: 1, isSystem: true, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: null,
    subCategories: [
      { id: 'groceries-supermarket', name: 'Supermarket', color: '#10b981', icon: 'pi pi-shop', sortOrder: 1, isSystem: false, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: 'groceries', subCategories: [] }
    ]
  },
  {
    id: 'fuel', name: 'Fuel', color: '#3b82f6', icon: 'pi pi-car', sortOrder: 2, isSystem: true, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: null,
    subCategories: []
  },
  {
    id: 'income', name: 'Income', color: '#8b5cf6', icon: 'pi pi-wallet', sortOrder: 3, isSystem: true, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: null,
    subCategories: []
  }
];

const DIRECTION_OPTIONS = ['Debit', 'Credit'];
const TRANSACTION_TYPE_OPTIONS = ['Purchase', 'Transfer', 'Withdrawal', 'Fee', 'Adjustment'];

@Component({
  standalone: true,
  selector: 'app-transactions',
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    SidebarModule,
    TableModule,
    TagModule
  ],
  template: `
    <p-card header="Transactions" subheader="Review ledger activity, refine filters, and handle recategorization workflows.">
      <div class="toolbar mb-3">
        <div class="flex flex-wrap gap-2 align-items-center">
          <p-button label="Filters" icon="pi pi-filter" severity="secondary" [outlined]="true" (onClick)="filtersSidebarVisible = true"></p-button>
          <p-button label="Manual entry" icon="pi pi-plus" (onClick)="openManualEntryDialog()"></p-button>
          <p-button
            label="Bulk recategorize"
            icon="pi pi-tags"
            severity="secondary"
            [outlined]="true"
            [disabled]="selectedTransactions.length === 0"
            (onClick)="openBulkRecategorizeDialog()"></p-button>
        </div>

        <div class="flex flex-wrap gap-2 align-items-center justify-content-end">
          <span class="text-sm text-color-secondary">{{ visibleTransactions().length }} of {{ transactions().totalCount }} rows on this page</span>
          <p-button label="Export CSV" icon="pi pi-download" severity="contrast" [outlined]="true" [loading]="exporting" (onClick)="exportCurrentView()"></p-button>
        </div>
      </div>

      <p-table
        [value]="visibleTransactions()"
        dataKey="id"
        [(selection)]="selectedTransactions"
        [lazy]="true"
        [paginator]="true"
        [rows]="rows"
        [first]="first"
        [totalRecords]="effectiveTotalRecords()"
        [loading]="loading"
        responsiveLayout="scroll"
        (onLazyLoad)="onLazyLoad($event)">
        <ng-template pTemplate="header">
          <tr>
            <th style="width: 3rem"><p-tableHeaderCheckbox></p-tableHeaderCheckbox></th>
            <th>Date</th>
            <th>Merchant</th>
            <th>Category</th>
            <th>Direction</th>
            <th>Source</th>
            <th>Amount</th>
            <th style="width: 12rem">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-transaction>
          <tr>
            <td><p-tableCheckbox [value]="transaction"></p-tableCheckbox></td>
            <td>{{ transaction.transactionDate | date:'mediumDate' }}</td>
            <td>
              <div class="font-medium">{{ transaction.merchantNormalized || transaction.merchantRaw || 'Manual entry' }}</div>
              <small class="text-color-secondary">{{ transaction.notes || 'No notes' }}</small>
            </td>
            <td>
              <div>{{ transaction.parentCategoryName || transaction.categoryName }}</div>
              <small class="text-color-secondary">{{ transaction.categorySource }}</small>
            </td>
            <td><p-tag [severity]="transaction.direction === 'Credit' ? 'success' : 'warn'" [value]="transaction.direction"></p-tag></td>
            <td><p-tag [value]="transaction.transactionSource"></p-tag></td>
            <td>{{ transaction.amount | currency:transaction.currency:'symbol':'1.2-2' }}</td>
            <td>
              <div class="flex flex-wrap gap-2">
                <p-button icon="pi pi-pencil" size="small" [text]="true" (onClick)="openEditDialog(transaction)"></p-button>
                <p-button icon="pi pi-tags" size="small" [text]="true" severity="secondary" (onClick)="openSingleRecategorizeDialog(transaction)"></p-button>
                <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" (onClick)="confirmDelete(transaction)"></p-button>
              </div>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="8" class="text-center py-5 text-color-secondary">No transactions match the current filters.</td>
          </tr>
        </ng-template>
      </p-table>
    </p-card>

    <p-sidebar [(visible)]="filtersSidebarVisible" position="right" [dismissible]="false" styleClass="w-full md:w-25rem">
      <ng-template pTemplate="header">
        <div class="font-semibold text-lg">Filters</div>
      </ng-template>

      <div class="p-fluid flex flex-column gap-3 mt-2">
        <div class="grid">
          <div class="col-6">
            <label class="field-label" for="fromDate">From</label>
            <input id="fromDate" type="date" pInputText [(ngModel)]="filtersDraft.from" />
          </div>
          <div class="col-6">
            <label class="field-label" for="toDate">To</label>
            <input id="toDate" type="date" pInputText [(ngModel)]="filtersDraft.to" />
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
            [(ngModel)]="filtersDraft.categoryIds"></p-multiSelect>
        </div>

        <div>
          <label class="field-label" for="merchantFilter">Merchant</label>
          <input id="merchantFilter" type="text" pInputText [(ngModel)]="filtersDraft.merchant" placeholder="Search merchant" />
        </div>

        <div class="grid">
          <div class="col-6">
            <label class="field-label" for="minAmount">Min amount</label>
            <input id="minAmount" type="number" step="0.01" pInputText [(ngModel)]="filtersDraft.minAmount" />
          </div>
          <div class="col-6">
            <label class="field-label" for="maxAmount">Max amount</label>
            <input id="maxAmount" type="number" step="0.01" pInputText [(ngModel)]="filtersDraft.maxAmount" />
          </div>
        </div>

        <div>
          <label class="field-label" for="directionFilter">Direction</label>
          <select id="directionFilter" class="native-select" [(ngModel)]="filtersDraft.direction">
            <option value="">All directions</option>
            <option *ngFor="let option of directionOptions" [value]="option">{{ option }}</option>
          </select>
        </div>

        <div class="flex flex-wrap justify-content-end gap-2 pt-2">
          <p-button label="Reset" severity="secondary" [outlined]="true" (onClick)="resetFilters()"></p-button>
          <p-button label="Apply filters" icon="pi pi-check" (onClick)="applyFilters()"></p-button>
        </div>
      </div>
    </p-sidebar>

    <p-dialog [(visible)]="transactionDialogVisible" [modal]="true" [style]="{ width: 'min(38rem, 95vw)' }" [header]="editingTransactionId ? 'Edit transaction' : 'Manual transaction entry'">
      <div class="p-fluid grid mt-1">
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnAmount">Amount</label>
          <input id="txnAmount" type="number" step="0.01" pInputText [(ngModel)]="transactionForm.amount" />
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnDate">Transaction date</label>
          <input id="txnDate" type="date" pInputText [(ngModel)]="transactionForm.transactionDate" />
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnDirection">Direction</label>
          <select id="txnDirection" class="native-select" [(ngModel)]="transactionForm.direction">
            <option *ngFor="let option of directionOptions" [value]="option">{{ option }}</option>
          </select>
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="txnType">Type</label>
          <select id="txnType" class="native-select" [(ngModel)]="transactionForm.transactionType">
            <option *ngFor="let option of transactionTypeOptions" [value]="option">{{ option }}</option>
          </select>
        </div>
        <div class="col-12">
          <label class="field-label" for="txnMerchant">Merchant</label>
          <input id="txnMerchant" type="text" pInputText [(ngModel)]="transactionForm.merchantRaw" placeholder="e.g. MERCATOR" />
        </div>
        <div class="col-12">
          <label class="field-label" for="txnCategory">Category</label>
          <select id="txnCategory" class="native-select" [(ngModel)]="transactionForm.categoryId">
            <option *ngFor="let option of categoryOptions()" [value]="option.value">{{ option.label }}</option>
          </select>
        </div>
        <div class="col-12">
          <label class="field-label" for="txnNotes">Notes</label>
          <input id="txnNotes" type="text" pInputText [(ngModel)]="transactionForm.notes" placeholder="Optional notes" />
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Cancel" severity="secondary" [outlined]="true" (onClick)="transactionDialogVisible = false"></p-button>
          <p-button label="Save transaction" icon="pi pi-save" [loading]="savingTransaction" (onClick)="saveTransaction()"></p-button>
        </div>
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="recategorizeDialogVisible" [modal]="true" [style]="{ width: 'min(28rem, 95vw)' }" [header]="selectedTransactions.length > 1 ? 'Bulk recategorize transactions' : 'Recategorize transaction'">
      <div class="p-fluid flex flex-column gap-3 mt-1">
        <div>
          <label class="field-label" for="recategorizeCategory">New category</label>
          <select id="recategorizeCategory" class="native-select" [(ngModel)]="recategorizeCategoryId">
            <option value="" disabled>Select a category</option>
            <option *ngFor="let option of categoryOptions()" [value]="option.value">{{ option.label }}</option>
          </select>
        </div>
        <div class="flex align-items-center gap-2">
          <p-checkbox inputId="createRule" [(ngModel)]="createMerchantRule" [binary]="true"></p-checkbox>
          <label for="createRule">Create or refresh a merchant rule while recategorizing</label>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Cancel" severity="secondary" [outlined]="true" (onClick)="recategorizeDialogVisible = false"></p-button>
          <p-button label="Apply category" icon="pi pi-check" [loading]="savingRecategorization" (onClick)="applyRecategorization()"></p-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 1rem;
    }

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
export class TransactionsComponent {
  private readonly apiService = inject(ApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly messageService = inject(MessageService);

  protected readonly transactions = signal<PagedResult<Transaction>>(TRANSACTIONS_FALLBACK);
  protected readonly categories = signal<Category[]>(CATEGORIES_FALLBACK);
  protected readonly categoryOptions = computed(() => this.flattenCategories(this.categories()).map((category) => ({
    label: category.parentCategoryId ? `${this.resolveParentCategoryName(category.parentCategoryId)} › ${category.name}` : category.name,
    value: category.id
  })));
  protected readonly visibleTransactions = computed(() => {
    const categoryIds = this.appliedFilters.categoryIds;
    if (categoryIds.length === 0) {
      return this.transactions().items;
    }

    return this.transactions().items.filter((transaction) => categoryIds.includes(transaction.categoryId));
  });
  protected readonly effectiveTotalRecords = computed(() => this.appliedFilters.categoryIds.length === 0 ? this.transactions().totalCount : this.visibleTransactions().length);

  protected readonly directionOptions = DIRECTION_OPTIONS;
  protected readonly transactionTypeOptions = TRANSACTION_TYPE_OPTIONS;

  protected filtersSidebarVisible = false;
  protected transactionDialogVisible = false;
  protected recategorizeDialogVisible = false;
  protected selectedTransactions: Transaction[] = [];
  protected loading = false;
  protected exporting = false;
  protected savingTransaction = false;
  protected savingRecategorization = false;
  protected first = 0;
  protected rows = 10;
  protected editingTransactionId: string | null = null;
  protected recategorizeCategoryId = '';
  protected createMerchantRule = true;
  protected activeRecategorizeTransaction: Transaction | null = null;

  protected readonly filtersDraft: TransactionFilterState = this.createEmptyFilters();
  private appliedFilters: TransactionFilterState = this.createEmptyFilters();

  protected transactionForm: TransactionFormModel = this.createEmptyTransactionForm();

  constructor() {
    this.loadCategories();
    this.loadTransactions(1, this.rows);
  }

  protected onLazyLoad(event: { first?: number | null; rows?: number | null } | undefined): void {
    this.first = event?.first ?? this.first;
    this.rows = event?.rows ?? this.rows;
    this.loadTransactions(this.getCurrentPage(), this.rows);
  }

  protected applyFilters(): void {
    this.appliedFilters = { ...this.filtersDraft, categoryIds: [...this.filtersDraft.categoryIds] };
    this.first = 0;
    this.filtersSidebarVisible = false;
    this.loadTransactions(1, this.rows);
  }

  protected resetFilters(): void {
    Object.assign(this.filtersDraft, this.createEmptyFilters());
    this.appliedFilters = this.createEmptyFilters();
    this.first = 0;
    this.loadTransactions(1, this.rows);
  }

  protected openManualEntryDialog(): void {
    this.editingTransactionId = null;
    this.transactionForm = this.createEmptyTransactionForm();
    this.transactionDialogVisible = true;
  }

  protected openEditDialog(transaction: Transaction): void {
    this.editingTransactionId = transaction.id;
    this.transactionForm = {
      amount: transaction.amount,
      currency: transaction.currency,
      direction: transaction.direction,
      transactionType: transaction.transactionType,
      transactionDate: this.toDateInputValue(transaction.transactionDate),
      merchantRaw: transaction.merchantRaw ?? transaction.merchantNormalized ?? '',
      categoryId: transaction.categoryId,
      notes: transaction.notes ?? ''
    };
    this.transactionDialogVisible = true;
  }

  protected saveTransaction(): void {
    if (!this.transactionForm.amount || !this.transactionForm.categoryId || !this.transactionForm.transactionDate) {
      this.messageService.add({ severity: 'warn', summary: 'Missing fields', detail: 'Amount, date, and category are required.' });
      return;
    }

    this.savingTransaction = true;
    const basePayload: CreateTransactionRequest = {
      amount: Number(this.transactionForm.amount),
      direction: this.transactionForm.direction,
      transactionType: this.transactionForm.transactionType,
      transactionDate: new Date(this.transactionForm.transactionDate).toISOString(),
      merchantRaw: this.transactionForm.merchantRaw.trim() || null,
      categoryId: this.transactionForm.categoryId,
      notes: this.transactionForm.notes.trim() || null
    };

    const request$ = this.editingTransactionId
      ? this.apiService.updateTransaction(this.editingTransactionId, {
          amount: basePayload.amount,
          direction: basePayload.direction,
          transactionType: basePayload.transactionType,
          transactionDate: basePayload.transactionDate,
          merchantRaw: basePayload.merchantRaw,
          categoryId: basePayload.categoryId,
          notes: basePayload.notes,
          currency: this.transactionForm.currency
        } as UpdateTransactionRequest)
      : this.apiService.createTransaction(basePayload);

    request$.pipe(
      catchError(() => of(this.buildLocalTransaction(basePayload, this.editingTransactionId))),
      finalize(() => this.savingTransaction = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((transaction) => {
      this.upsertTransaction(transaction);
      this.transactionDialogVisible = false;
      this.messageService.add({
        severity: 'success',
        summary: this.editingTransactionId ? 'Transaction updated' : 'Transaction created',
        detail: 'The transaction list has been refreshed.'
      });
      this.editingTransactionId = null;
    });
  }

  protected confirmDelete(transaction: Transaction): void {
    this.confirmationService.confirm({
      header: 'Delete transaction',
      message: `Delete ${transaction.merchantNormalized || transaction.merchantRaw || 'this transaction'}?`,
      accept: () => {
        this.apiService.deleteTransaction(transaction.id).pipe(
          catchError(() => of(void 0)),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => {
          this.transactions.update((current) => ({
            ...current,
            items: current.items.filter((item) => item.id !== transaction.id),
            totalCount: Math.max(current.totalCount - 1, 0)
          }));
          this.selectedTransactions = this.selectedTransactions.filter((item) => item.id !== transaction.id);
          this.messageService.add({ severity: 'success', summary: 'Transaction deleted', detail: 'The transaction was removed from the ledger.' });
        });
      }
    });
  }

  protected openSingleRecategorizeDialog(transaction: Transaction): void {
    this.activeRecategorizeTransaction = transaction;
    this.recategorizeCategoryId = transaction.categoryId;
    this.createMerchantRule = true;
    this.recategorizeDialogVisible = true;
  }

  protected openBulkRecategorizeDialog(): void {
    if (this.selectedTransactions.length === 0) {
      return;
    }

    this.activeRecategorizeTransaction = null;
    this.recategorizeCategoryId = this.selectedTransactions[0]?.categoryId ?? this.categoryOptions()[0]?.value ?? '';
    this.createMerchantRule = true;
    this.recategorizeDialogVisible = true;
  }

  protected applyRecategorization(): void {
    if (!this.recategorizeCategoryId) {
      this.messageService.add({ severity: 'warn', summary: 'Choose a category', detail: 'Pick a target category before saving.' });
      return;
    }

    this.savingRecategorization = true;
    const payload: RecategorizeTransactionRequest = {
      categoryId: this.recategorizeCategoryId,
      createMerchantRule: this.createMerchantRule
    };

    const request$: import('rxjs').Observable<Transaction | null> = this.activeRecategorizeTransaction
      ? this.apiService.recategorizeTransaction(this.activeRecategorizeTransaction.id, payload).pipe(
          catchError(() => of(this.buildRecategorizedTransaction(this.activeRecategorizeTransaction!, payload.categoryId)))
        )
      : this.apiService.bulkRecategorize({
          transactionIds: this.selectedTransactions.map((transaction) => transaction.id),
          categoryId: payload.categoryId,
          createMerchantRule: payload.createMerchantRule
        }).pipe(
          map(() => null),
          catchError(() => of(null))
        );

    request$.pipe(
      finalize(() => this.savingRecategorization = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((transaction) => {
      if (transaction) {
        this.upsertTransaction(transaction);
      } else {
        this.transactions.update((current) => ({
          ...current,
          items: current.items.map((item) => this.selectedTransactions.some((selected) => selected.id === item.id)
            ? this.buildRecategorizedTransaction(item, payload.categoryId)
            : item)
        }));
      }

      this.recategorizeDialogVisible = false;
      this.selectedTransactions = [];
      this.messageService.add({ severity: 'success', summary: 'Categories updated', detail: 'The selected transactions were recategorized.' });
    });
  }

  protected exportCurrentView(): void {
    this.exporting = true;
    this.apiService.exportTransactions(this.buildServerFilters()).pipe(
      catchError(() => of(new Blob([this.createFallbackCsv()], { type: 'text/csv;charset=utf-8' }))),
      finalize(() => this.exporting = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = this.document.createElement('a');
      link.href = url;
      link.download = 'transactions.csv';
      link.click();
      URL.revokeObjectURL(url);
      this.messageService.add({ severity: 'success', summary: 'Export ready', detail: 'Transactions were exported to CSV.' });
    });
  }

  private loadTransactions(page: number, pageSize: number): void {
    this.loading = true;
    this.apiService.getTransactions(this.buildServerFilters(page, pageSize)).pipe(
      catchError(() => of({ ...TRANSACTIONS_FALLBACK, page, pageSize })),
      finalize(() => this.loading = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((transactions) => {
      this.transactions.set(transactions);
      this.selectedTransactions = [];
    });
  }

  private loadCategories(): void {
    this.apiService.getCategories().pipe(
      catchError(() => of(CATEGORIES_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((categories) => {
      this.categories.set(categories);
      if (!this.transactionForm.categoryId) {
        this.transactionForm.categoryId = this.categoryOptions()[0]?.value ?? '';
      }
    });
  }

  private buildServerFilters(page?: number, pageSize?: number): TransactionFilters {
    const filters: TransactionFilters = {
      from: this.appliedFilters.from || undefined,
      to: this.appliedFilters.to || undefined,
      merchant: this.appliedFilters.merchant || undefined,
      minAmount: this.toNumber(this.appliedFilters.minAmount),
      maxAmount: this.toNumber(this.appliedFilters.maxAmount),
      direction: this.appliedFilters.direction || undefined,
      page,
      pageSize
    };

    if (this.appliedFilters.categoryIds.length === 1) {
      filters.categoryId = this.appliedFilters.categoryIds[0];
    }

    return filters;
  }

  private upsertTransaction(transaction: Transaction): void {
    this.transactions.update((current) => {
      const exists = current.items.some((item) => item.id === transaction.id);
      const items = exists
        ? current.items.map((item) => item.id === transaction.id ? transaction : item)
        : [transaction, ...current.items];

      return {
        ...current,
        items,
        totalCount: exists ? current.totalCount : current.totalCount + 1
      };
    });
  }

  private buildLocalTransaction(payload: CreateTransactionRequest, id?: string | null): Transaction {
    const category = this.findCategory(payload.categoryId);
    return {
      id: id ?? `local-${Date.now()}`,
      userId: 'local-user',
      amount: payload.amount,
      currency: 'EUR',
      direction: payload.direction,
      transactionType: payload.transactionType,
      transactionDate: payload.transactionDate,
      merchantRaw: payload.merchantRaw,
      merchantNormalized: payload.merchantRaw?.toUpperCase() ?? null,
      categoryId: payload.categoryId,
      categorySource: 'Manual',
      transactionSource: id ? 'Manual' : 'Manual',
      notes: payload.notes,
      isDeleted: false,
      rawMessageId: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      categoryName: category?.name ?? 'Uncategorized',
      parentCategoryName: category?.parentCategoryId ? this.resolveParentCategoryName(category.parentCategoryId) : null
    };
  }

  private buildRecategorizedTransaction(transaction: Transaction, categoryId: string): Transaction {
    const category = this.findCategory(categoryId);
    return {
      ...transaction,
      categoryId,
      categoryName: category?.name ?? transaction.categoryName,
      parentCategoryName: category?.parentCategoryId ? this.resolveParentCategoryName(category.parentCategoryId) : null,
      categorySource: 'Manual',
      updatedAt: new Date().toISOString()
    };
  }

  private createFallbackCsv(): string {
    const rows = this.visibleTransactions().map((transaction) => [
      transaction.transactionDate,
      transaction.merchantNormalized || transaction.merchantRaw || '',
      transaction.parentCategoryName || transaction.categoryName,
      transaction.direction,
      transaction.amount.toFixed(2)
    ].join(','));

    return ['date,merchant,category,direction,amount', ...rows].join('\n');
  }

  private createEmptyFilters(): TransactionFilterState {
    return {
      from: '',
      to: '',
      categoryIds: [],
      merchant: '',
      minAmount: '',
      maxAmount: '',
      direction: ''
    };
  }

  private createEmptyTransactionForm(): TransactionFormModel {
    return {
      amount: null,
      currency: 'EUR',
      direction: 'Debit',
      transactionType: 'Purchase',
      transactionDate: this.toDateInputValue(new Date().toISOString()),
      merchantRaw: '',
      categoryId: this.categoryOptions()[0]?.value ?? '',
      notes: ''
    };
  }

  private flattenCategories(categories: Category[]): Category[] {
    return categories.flatMap((category) => [category, ...this.flattenCategories(category.subCategories ?? [])]);
  }

  private findCategory(categoryId: string): Category | undefined {
    return this.flattenCategories(this.categories()).find((category) => category.id === categoryId);
  }

  private resolveParentCategoryName(categoryId: string): string {
    return this.flattenCategories(this.categories()).find((category) => category.id === categoryId)?.name ?? 'Parent category';
  }

  private toNumber(value: string): number | undefined {
    if (value === '') {
      return undefined;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : undefined;
  }

  private toDateInputValue(value: string): string {
    return value.slice(0, 10);
  }

  private getCurrentPage(): number {
    return Math.floor(this.first / this.rows) + 1;
  }
}

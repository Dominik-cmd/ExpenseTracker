import { CurrencyPipe, DatePipe, DOCUMENT } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, map, of, Subject, switchMap } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SidebarModule } from 'primeng/sidebar';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { EMPTY_CATEGORIES, EMPTY_PAGED_RESULT } from '../../core/constants/fallbacks';
import { Category, CreateTransactionRequest, PagedResult, RecategorizeTransactionRequest, Transaction, TransactionFilters, UpdateTransactionRequest } from '../../core/models';
import { CategoryService } from '../../core/services/category.service';
import { TransactionService } from '../../core/services/transaction.service';
import { TransactionDialogComponent, TransactionDialogSaveEvent } from './transaction-dialog.component';
import { TransactionFiltersComponent, TransactionFilterState } from './transaction-filters.component';
import { TransactionRecategorizeComponent, TransactionRecategorizeEvent } from './transaction-recategorize.component';

const DIRECTION_OPTIONS = ['Debit', 'Credit'];
const TRANSACTION_TYPE_OPTIONS = ['Purchase', 'Transfer', 'Withdrawal', 'Fee', 'Adjustment'];

@Component({
  standalone: true,
  selector: 'app-transactions',
  imports: [DatePipe, CurrencyPipe, ButtonModule, CardModule, SidebarModule, TableModule, TagModule, TransactionFiltersComponent, TransactionDialogComponent, TransactionRecategorizeComponent],
  template: `
    <p-card header="Transactions" subheader="Review ledger activity, refine filters, and handle recategorization workflows.">
      <div class="toolbar mb-3">
        <div class="flex flex-wrap gap-2 align-items-center">
          <p-button label="Filters" icon="pi pi-filter" severity="secondary" [outlined]="true" (onClick)="filtersSidebarVisible.set(true)"></p-button>
          <p-button label="Manual entry" icon="pi pi-plus" (onClick)="openManualEntryDialog()"></p-button>
          <p-button label="Bulk recategorize" icon="pi pi-tags" severity="secondary" [outlined]="true" [disabled]="selectedTransactions.length === 0" (onClick)="openBulkRecategorizeDialog()"></p-button>
        </div>
        <div class="flex flex-wrap gap-2 align-items-center justify-content-end">
          <span class="text-sm text-color-secondary">{{ visibleTransactions().length }} of {{ transactions().totalCount }} rows on this page</span>
          <p-button label="Export CSV" icon="pi pi-download" severity="contrast" [outlined]="true" [loading]="exporting()" (onClick)="exportCurrentView()"></p-button>
        </div>
      </div>

      <p-table [value]="visibleTransactions()" dataKey="id" [(selection)]="selectedTransactions" [lazy]="true" [paginator]="true" [rows]="rows" [rowsPerPageOptions]="[10, 25, 100]" [first]="first" [totalRecords]="effectiveTotalRecords()" [loading]="loading()" responsiveLayout="scroll" (onLazyLoad)="onLazyLoad($event)">
        <ng-template pTemplate="header">
          <tr>
            <th style="width: 3rem"><p-tableHeaderCheckbox></p-tableHeaderCheckbox></th>
            <th>Date</th><th>Merchant</th><th>Category</th><th>Direction</th><th>Source</th><th>Amount</th><th style="width: 12rem">Actions</th>
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
        <ng-template pTemplate="emptymessage"><tr><td colspan="8" class="text-center py-5 text-color-secondary">No transactions match the current filters.</td></tr></ng-template>
      </p-table>
    </p-card>

    <p-sidebar [visible]="filtersSidebarVisible()" position="right" [dismissible]="false" styleClass="w-full md:w-25rem" (visibleChange)="filtersSidebarVisible.set($event)">
      <ng-template pTemplate="header"><div class="font-semibold text-lg">Filters</div></ng-template>
      <app-transaction-filters [filterState]="filters()" [categories]="categoryOptions()" [directionOptions]="directionOptions" (applyFilters)="applyFilters($event)" (clearFilters)="resetFilters()"></app-transaction-filters>
    </p-sidebar>

    <app-transaction-dialog [visible]="transactionDialogVisible()" [editingTransaction]="editingTransaction()" [categories]="categoryOptions()" [directionOptions]="directionOptions" [transactionTypeOptions]="transactionTypeOptions" [saving]="savingTransaction()" (save)="saveTransaction($event)" (cancel)="closeTransactionDialog()"></app-transaction-dialog>
    <app-transaction-recategorize [visible]="recategorizeDialogVisible()" [transaction]="activeRecategorizeTransaction()" [categories]="categoryOptions()" [initialCategoryId]="recategorizeInitialCategoryId()" [saving]="savingRecategorization()" (recategorize)="applyRecategorization($event)" (cancel)="closeRecategorizeDialog()"></app-transaction-recategorize>
  `,
  styles: [`.toolbar { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 1rem; }`]
})
export class TransactionsComponent {
  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly messageService = inject(MessageService);

  protected readonly transactions = signal<PagedResult<Transaction>>(EMPTY_PAGED_RESULT<Transaction>());
  protected readonly categories = signal<Category[]>(EMPTY_CATEGORIES);
  protected readonly filters = signal<TransactionFilterState>(this.createEmptyFilters());
  protected readonly filtersSidebarVisible = signal(false);
  protected readonly transactionDialogVisible = signal(false);
  protected readonly recategorizeDialogVisible = signal(false);
  protected readonly editingTransaction = signal<Transaction | null>(null);
  protected readonly activeRecategorizeTransaction = signal<Transaction | null>(null);
  protected readonly recategorizeInitialCategoryId = signal('');
  protected readonly loading = signal(false);
  protected readonly exporting = signal(false);
  protected readonly savingTransaction = signal(false);
  protected readonly savingRecategorization = signal(false);
  protected readonly categoryOptions = computed(() => this.flattenCategories(this.categories()).map((category) => ({
    label: category.parentCategoryId ? `${this.resolveParentCategoryName(category.parentCategoryId)} › ${category.name}` : category.name,
    value: category.id
  })));
  protected readonly visibleTransactions = computed(() => this.transactions().items);
  protected readonly effectiveTotalRecords = computed(() => this.transactions().totalCount);
  protected readonly directionOptions = DIRECTION_OPTIONS;
  protected readonly transactionTypeOptions = TRANSACTION_TYPE_OPTIONS;

  protected selectedTransactions: Transaction[] = [];
  protected first = 0;
  protected rows = 10;

  private readonly loadTrigger$ = new Subject<{ page: number; pageSize: number }>();

  constructor() {
    this.loadCategories();
    this.loadTrigger$
      .pipe(
        switchMap(({ page, pageSize }) => {
          this.loading.set(true);
          return this.transactionService.getTransactions(this.buildServerFilters(page, pageSize)).pipe(catchError(() => of({ ...EMPTY_PAGED_RESULT<Transaction>(), page, pageSize })));
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((transactions) => {
        this.loading.set(false);
        this.transactions.set(transactions);
        this.selectedTransactions = [];
      });
    this.loadTransactions(1, this.rows);
  }

  protected onLazyLoad(event: { first?: number | null; rows?: number | null } | undefined): void {
    this.first = event?.first ?? this.first;
    this.rows = event?.rows ?? this.rows;
    this.loadTransactions(this.getCurrentPage(), this.rows);
  }

  protected applyFilters(filters: TransactionFilterState): void {
    this.filters.set({ ...filters, categoryIds: [...filters.categoryIds] });
    this.first = 0;
    this.filtersSidebarVisible.set(false);
    this.loadTransactions(1, this.rows);
  }

  protected resetFilters(): void {
    this.filters.set(this.createEmptyFilters());
    this.first = 0;
    this.loadTransactions(1, this.rows);
  }

  protected openManualEntryDialog(): void {
    this.editingTransaction.set(null);
    this.transactionDialogVisible.set(true);
  }

  protected openEditDialog(transaction: Transaction): void {
    this.editingTransaction.set(transaction);
    this.transactionDialogVisible.set(true);
  }

  protected closeTransactionDialog(): void {
    this.transactionDialogVisible.set(false);
    this.editingTransaction.set(null);
  }

  protected saveTransaction(form: TransactionDialogSaveEvent): void {
    if (!form.amount || !form.categoryId || !form.transactionDate) {
      this.messageService.add({ severity: 'warn', summary: 'Missing fields', detail: 'Amount, date, and category are required.' });
      return;
    }

    const editingTransaction = this.editingTransaction();
    const basePayload: CreateTransactionRequest = {
      amount: Number(form.amount),
      direction: form.direction,
      transactionType: form.transactionType,
      transactionDate: new Date(form.transactionDate).toISOString(),
      merchantRaw: form.merchantRaw.trim() || null,
      categoryId: form.categoryId,
      notes: form.notes.trim() || null
    };

    this.savingTransaction.set(true);
    const request$ = editingTransaction
      ? this.transactionService.updateTransaction(editingTransaction.id, { ...basePayload, currency: form.currency } as UpdateTransactionRequest)
      : this.transactionService.createTransaction(basePayload);

    request$
      .pipe(finalize(() => this.savingTransaction.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (transaction) => {
          this.upsertTransaction(transaction);
          this.closeTransactionDialog();
          this.messageService.add({ severity: 'success', summary: editingTransaction ? 'Transaction updated' : 'Transaction created', detail: 'The transaction list has been refreshed.' });
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Save failed', detail: 'The transaction could not be saved. Please try again.' });
        }
      });
  }

  protected confirmDelete(transaction: Transaction): void {
    this.confirmationService.confirm({
      header: 'Delete transaction',
      message: `Delete ${transaction.merchantNormalized || transaction.merchantRaw || 'this transaction'}?`,
      accept: () => {
        this.transactionService.deleteTransaction(transaction.id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.transactions.update((current) => ({ ...current, items: current.items.filter((item) => item.id !== transaction.id), totalCount: Math.max(current.totalCount - 1, 0) }));
              this.selectedTransactions = this.selectedTransactions.filter((item) => item.id !== transaction.id);
              this.messageService.add({ severity: 'success', summary: 'Transaction deleted', detail: 'The transaction was removed from the ledger.' });
            },
            error: () => {
              this.messageService.add({ severity: 'error', summary: 'Delete failed', detail: 'The transaction could not be deleted. Please try again.' });
            }
          });
      }
    });
  }

  protected openSingleRecategorizeDialog(transaction: Transaction): void {
    this.openRecategorizeDialog(transaction, transaction.categoryId);
  }

  protected openBulkRecategorizeDialog(): void {
    if (this.selectedTransactions.length === 0) return;
    this.openRecategorizeDialog(null, this.selectedTransactions[0]?.categoryId ?? this.categoryOptions()[0]?.value ?? '');
  }

  protected closeRecategorizeDialog(): void {
    this.recategorizeDialogVisible.set(false);
    this.activeRecategorizeTransaction.set(null);
    this.recategorizeInitialCategoryId.set('');
  }

  protected applyRecategorization(event: TransactionRecategorizeEvent): void {
    if (!event.categoryId) {
      this.messageService.add({ severity: 'warn', summary: 'Choose a category', detail: 'Pick a target category before saving.' });
      return;
    }

    const activeRecategorizeTransaction = this.activeRecategorizeTransaction();
    const payload: RecategorizeTransactionRequest = { categoryId: event.categoryId, createMerchantRule: event.createMerchantRule };
    const request$: import('rxjs').Observable<Transaction | null> = activeRecategorizeTransaction
      ? this.transactionService.recategorizeTransaction(activeRecategorizeTransaction.id, payload)
      : this.transactionService.bulkRecategorize({ transactionIds: this.selectedTransactions.map((transaction) => transaction.id), categoryId: payload.categoryId, createMerchantRule: payload.createMerchantRule }).pipe(map(() => null));

    this.savingRecategorization.set(true);
    request$
      .pipe(finalize(() => this.savingRecategorization.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (transaction) => {
          if (transaction) this.upsertTransaction(transaction);
          else this.transactions.update((current) => ({
            ...current,
            items: current.items.map((item) => this.selectedTransactions.some((selected) => selected.id === item.id) ? this.buildRecategorizedTransaction(item, payload.categoryId) : item)
          }));

          this.closeRecategorizeDialog();
          this.selectedTransactions = [];
          this.messageService.add({ severity: 'success', summary: 'Categories updated', detail: 'The selected transactions were recategorized.' });
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Recategorization failed', detail: 'Could not recategorize the selected transactions. Please try again.' });
        }
      });
  }

  protected exportCurrentView(): void {
    this.exporting.set(true);
    this.transactionService.exportTransactions(this.buildServerFilters())
      .pipe(catchError(() => of(new Blob([this.createFallbackCsv()], { type: 'text/csv;charset=utf-8' }))), finalize(() => this.exporting.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe((blob) => {
        const url = URL.createObjectURL(blob);
        const link = this.document.createElement('a');
        link.href = url;
        link.download = 'transactions.csv';
        link.click();
        URL.revokeObjectURL(url);
        this.messageService.add({ severity: 'success', summary: 'Export ready', detail: 'Transactions were exported to CSV.' });
      });
  }

  private openRecategorizeDialog(transaction: Transaction | null, categoryId: string): void {
    this.activeRecategorizeTransaction.set(transaction);
    this.recategorizeInitialCategoryId.set(categoryId);
    this.recategorizeDialogVisible.set(true);
  }

  private loadTransactions(page: number, pageSize: number): void {
    this.loadTrigger$.next({ page, pageSize });
  }

  private loadCategories(): void {
    this.categoryService.getCategories().pipe(catchError(() => of(EMPTY_CATEGORIES)), takeUntilDestroyed(this.destroyRef)).subscribe((categories) => this.categories.set(categories));
  }

  private buildServerFilters(page?: number, pageSize?: number): TransactionFilters {
    const filters = this.filters();
    return {
      from: filters.from || undefined,
      to: filters.to || undefined,
      merchant: filters.merchant || undefined,
      minAmount: this.toNumber(filters.minAmount),
      maxAmount: this.toNumber(filters.maxAmount),
      direction: filters.direction || undefined,
      categoryIds: filters.categoryIds.length ? filters.categoryIds : undefined,
      page,
      pageSize
    };
  }

  private upsertTransaction(transaction: Transaction): void {
    this.transactions.update((current) => {
      const exists = current.items.some((item) => item.id === transaction.id);
      return { ...current, items: exists ? current.items.map((item) => item.id === transaction.id ? transaction : item) : [transaction, ...current.items], totalCount: exists ? current.totalCount : current.totalCount + 1 };
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
      transactionSource: 'Manual',
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
    return { ...transaction, categoryId, categoryName: category?.name ?? transaction.categoryName, parentCategoryName: category?.parentCategoryId ? this.resolveParentCategoryName(category.parentCategoryId) : null, categorySource: 'Manual', updatedAt: new Date().toISOString() };
  }

  private createFallbackCsv(): string {
    return ['date,merchant,category,direction,amount', ...this.visibleTransactions().map((transaction) => [
      transaction.transactionDate,
      transaction.merchantNormalized || transaction.merchantRaw || '',
      transaction.parentCategoryName || transaction.categoryName,
      transaction.direction,
      transaction.amount.toFixed(2)
    ].join(','))].join('\n');
  }

  private createEmptyFilters(): TransactionFilterState {
    return { from: '', to: '', categoryIds: [], merchant: '', minAmount: null, maxAmount: null, direction: '' };
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

  private toNumber(value: number | null | undefined): number | undefined {
    return value === null || value === undefined || !Number.isFinite(value) ? undefined : value;
  }

  private getCurrentPage(): number {
    return Math.floor(this.first / this.rows) + 1;
  }
}

import { DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { EMPTY_CATEGORIES, EMPTY_MERCHANT_RULES } from '../../core/constants/fallbacks';
import { Category, MerchantRule } from '../../core/models';
import { CategoryService } from '../../core/services/category.service';
import { MerchantRuleService } from '../../core/services/merchant-rule.service';

@Component({
  standalone: true,
  selector: 'app-merchant-rules',
  imports: [DatePipe, FormsModule, ButtonModule, CardModule, CheckboxModule, TableModule, TagModule],
  template: `
    <p-card header="Merchant rules" subheader="Inline tune the category each normalized merchant should map to.">
      <p-table [value]="rules()" responsiveLayout="scroll">
        <ng-template pTemplate="header">
          <tr>
            <th>Merchant</th>
            <th>Category</th>
            <th>Created by</th>
            <th>Hits</th>
            <th>Last hit</th>
            <th style="width: 10rem">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-rule>
          <tr>
            <td>
              <div class="font-medium">{{ rule.merchantNormalized }}</div>
              <small class="text-color-secondary">Created {{ rule.createdAt | date:'mediumDate' }}</small>
            </td>
            <td>
              @if (editingRuleId === rule.id) {
                <select class="native-select" [(ngModel)]="editingCategoryId">
                  @for (option of categoryOptions(); track option.id) {
                    <option [value]="option.id">{{ option.label }}</option>
                  }
                </select>
                <div class="flex align-items-center gap-2 mt-2">
                  <p-checkbox [(ngModel)]="editingApplyToExisting" [binary]="true" inputId="applyExisting"></p-checkbox>
                  <label for="applyExisting" class="text-sm" style="cursor:pointer">Apply to existing transactions</label>
                </div>
              } @else {
                {{ rule.parentCategoryName || rule.categoryName }}
              }
            </td>
            <td><p-tag [value]="rule.createdBy"></p-tag></td>
            <td>{{ rule.hitCount }}</td>
            <td>{{ rule.lastHitAt | date:'medium' }}</td>
            <td>
              <div class="flex flex-wrap gap-2">
                @if (editingRuleId === rule.id) {
                  <p-button icon="pi pi-check" size="small" [text]="true" [loading]="savingRuleId === rule.id" (onClick)="saveRule(rule)"></p-button>
                  <p-button icon="pi pi-times" size="small" [text]="true" severity="secondary" (onClick)="cancelEdit()"></p-button>
                } @else {
                  <p-button icon="pi pi-pencil" size="small" [text]="true" (onClick)="startEdit(rule)"></p-button>
                  <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" (onClick)="deleteRule(rule)"></p-button>
                }
              </div>
            </td>
          </tr>
        </ng-template>
      </p-table>
    </p-card>
  `,
  styles: [`
    .native-select {
      width: 100%;
      padding: 0.6rem 0.75rem;
      border: 1px solid var(--surface-border);
      border-radius: var(--border-radius);
      background: var(--surface-card);
      color: inherit;
    }
  `]
})
export class MerchantRulesComponent {
  private readonly merchantRuleService = inject(MerchantRuleService);
  private readonly categoryService = inject(CategoryService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly rules = signal<MerchantRule[]>(EMPTY_MERCHANT_RULES);
  protected readonly categories = signal<Category[]>(EMPTY_CATEGORIES);
  protected readonly categoryOptions = computed(() => this.flattenCategories(this.categories()).map((category) => ({
    id: category.id,
    label: category.parentCategoryId ? `${this.findCategoryName(category.parentCategoryId)} › ${category.name}` : category.name
  })));

  protected editingRuleId: string | null = null;
  protected editingCategoryId = '';
  protected editingApplyToExisting = false;
  protected savingRuleId: string | null = null;

  constructor() {
    this.loadRules();
    this.loadCategories();
  }

  protected startEdit(rule: MerchantRule): void {
    this.editingRuleId = rule.id;
    this.editingCategoryId = rule.categoryId;
    this.editingApplyToExisting = false;
  }

  protected cancelEdit(): void {
    this.editingRuleId = null;
    this.editingCategoryId = '';
    this.editingApplyToExisting = false;
  }

  protected saveRule(rule: MerchantRule): void {
    if (!this.editingCategoryId) {
      return;
    }

    this.savingRuleId = rule.id;
    this.merchantRuleService.updateMerchantRule(rule.id, { categoryId: this.editingCategoryId, applyToExistingTransactions: this.editingApplyToExisting }).pipe(
      catchError(() => of(this.buildLocalRule(rule, this.editingCategoryId))),
      finalize(() => this.savingRuleId = null),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((updatedRule) => {
      this.rules.update((rules) => rules.map((item) => item.id === updatedRule.id ? updatedRule : item));
      const detail = this.editingApplyToExisting
        ? `${updatedRule.merchantNormalized} remapped to ${updatedRule.parentCategoryName || updatedRule.categoryName} — all existing transactions updated.`
        : `${updatedRule.merchantNormalized} now maps to ${updatedRule.parentCategoryName || updatedRule.categoryName}.`;
      this.messageService.add({ severity: 'success', summary: 'Rule updated', detail });
      this.cancelEdit();
    });
  }

  protected deleteRule(rule: MerchantRule): void {
    this.confirmationService.confirm({
      header: 'Delete merchant rule',
      message: `Delete the rule for ${rule.merchantNormalized}?`,
      accept: () => {
        this.merchantRuleService.deleteMerchantRule(rule.id).pipe(
          catchError(() => of(void 0)),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => {
          this.rules.update((rules) => rules.filter((item) => item.id !== rule.id));
          this.messageService.add({ severity: 'success', summary: 'Rule deleted', detail: 'The merchant will no longer be auto-categorized by this rule.' });
        });
      }
    });
  }

  private loadRules(): void {
    this.merchantRuleService.getMerchantRules().pipe(
      catchError(() => of(EMPTY_MERCHANT_RULES)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((rules) => this.rules.set(rules));
  }

  private loadCategories(): void {
    this.categoryService.getCategories().pipe(
      catchError(() => of(EMPTY_CATEGORIES)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((categories) => this.categories.set(categories));
  }

  private buildLocalRule(rule: MerchantRule, categoryId: string): MerchantRule {
    const category = this.flattenCategories(this.categories()).find((item) => item.id === categoryId);
    return {
      ...rule,
      categoryId,
      categoryName: category?.name ?? rule.categoryName,
      parentCategoryName: category?.parentCategoryId ? this.findCategoryName(category.parentCategoryId) : null
    };
  }

  private flattenCategories(categories: Category[]): Category[] {
    return categories.flatMap((category) => [category, ...this.flattenCategories(category.subCategories ?? [])]);
  }

  private findCategoryName(categoryId: string): string {
    return this.flattenCategories(this.categories()).find((category) => category.id === categoryId)?.name ?? 'Category';
  }
}

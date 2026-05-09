import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { ApiService, Category, MerchantRule } from '../../core/services/api.service';

const RULES_FALLBACK: MerchantRule[] = [
  {
    id: '1', merchantNormalized: 'MERCATOR', categoryId: 'groceries', categoryName: 'Groceries', parentCategoryName: null,
    createdBy: 'llm', hitCount: 18, lastHitAt: new Date().toISOString(), createdAt: new Date(Date.now() - 86400000 * 20).toISOString()
  },
  {
    id: '2', merchantNormalized: 'OMV', categoryId: 'fuel', categoryName: 'Fuel', parentCategoryName: null,
    createdBy: 'manual', hitCount: 8, lastHitAt: new Date(Date.now() - 86400000).toISOString(), createdAt: new Date(Date.now() - 86400000 * 35).toISOString()
  }
];

const CATEGORIES_FALLBACK: Category[] = [
  { id: 'groceries', name: 'Groceries', color: '#10b981', icon: 'pi pi-shopping-basket', sortOrder: 1, isSystem: true, parentCategoryId: null, subCategories: [] },
  { id: 'fuel', name: 'Fuel', color: '#3b82f6', icon: 'pi pi-car', sortOrder: 2, isSystem: true, parentCategoryId: null, subCategories: [] },
  { id: 'subscriptions', name: 'Subscriptions', color: '#8b5cf6', icon: 'pi pi-bolt', sortOrder: 3, isSystem: false, parentCategoryId: null, subCategories: [] }
];

@Component({
  standalone: true,
  selector: 'app-merchant-rules',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, TableModule, TagModule],
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
              <ng-container *ngIf="editingRuleId === rule.id; else readonlyCategory">
                <select class="native-select" [(ngModel)]="editingCategoryId">
                  <option *ngFor="let option of categoryOptions()" [value]="option.id">{{ option.label }}</option>
                </select>
              </ng-container>
              <ng-template #readonlyCategory>{{ rule.parentCategoryName || rule.categoryName }}</ng-template>
            </td>
            <td><p-tag [value]="rule.createdBy"></p-tag></td>
            <td>{{ rule.hitCount }}</td>
            <td>{{ rule.lastHitAt | date:'medium' }}</td>
            <td>
              <div class="flex flex-wrap gap-2">
                <ng-container *ngIf="editingRuleId === rule.id; else actionButtons">
                  <p-button icon="pi pi-check" size="small" [text]="true" [loading]="savingRuleId === rule.id" (onClick)="saveRule(rule)"></p-button>
                  <p-button icon="pi pi-times" size="small" [text]="true" severity="secondary" (onClick)="cancelEdit()"></p-button>
                </ng-container>
                <ng-template #actionButtons>
                  <p-button icon="pi pi-pencil" size="small" [text]="true" (onClick)="startEdit(rule)"></p-button>
                  <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" (onClick)="deleteRule(rule)"></p-button>
                </ng-template>
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
  private readonly apiService = inject(ApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly rules = signal<MerchantRule[]>(RULES_FALLBACK);
  protected readonly categories = signal<Category[]>(CATEGORIES_FALLBACK);
  protected readonly categoryOptions = computed(() => this.flattenCategories(this.categories()).map((category) => ({
    id: category.id,
    label: category.parentCategoryId ? `${this.findCategoryName(category.parentCategoryId)} › ${category.name}` : category.name
  })));

  protected editingRuleId: string | null = null;
  protected editingCategoryId = '';
  protected savingRuleId: string | null = null;

  constructor() {
    this.loadRules();
    this.loadCategories();
  }

  protected startEdit(rule: MerchantRule): void {
    this.editingRuleId = rule.id;
    this.editingCategoryId = rule.categoryId;
  }

  protected cancelEdit(): void {
    this.editingRuleId = null;
    this.editingCategoryId = '';
  }

  protected saveRule(rule: MerchantRule): void {
    if (!this.editingCategoryId) {
      return;
    }

    this.savingRuleId = rule.id;
    this.apiService.updateMerchantRule(rule.id, { categoryId: this.editingCategoryId }).pipe(
      catchError(() => of(this.buildLocalRule(rule, this.editingCategoryId))),
      finalize(() => this.savingRuleId = null),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((updatedRule) => {
      this.rules.update((rules) => rules.map((item) => item.id === updatedRule.id ? updatedRule : item));
      this.messageService.add({ severity: 'success', summary: 'Rule updated', detail: `${updatedRule.merchantNormalized} now maps to ${updatedRule.parentCategoryName || updatedRule.categoryName}.` });
      this.cancelEdit();
    });
  }

  protected deleteRule(rule: MerchantRule): void {
    this.confirmationService.confirm({
      header: 'Delete merchant rule',
      message: `Delete the rule for ${rule.merchantNormalized}?`,
      accept: () => {
        this.apiService.deleteMerchantRule(rule.id).pipe(
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
    this.apiService.getMerchantRules().pipe(
      catchError(() => of(RULES_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((rules) => this.rules.set(rules));
  }

  private loadCategories(): void {
    this.apiService.getCategories().pipe(
      catchError(() => of(CATEGORIES_FALLBACK)),
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

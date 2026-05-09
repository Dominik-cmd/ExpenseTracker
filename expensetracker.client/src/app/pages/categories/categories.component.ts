import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { ApiService, Category, CreateCategoryRequest, UpdateCategoryRequest } from '../../core/services/api.service';

interface CategoryFormModel {
  name: string;
  color: string;
  icon: string;
  parentCategoryId: string | null;
}

const CATEGORIES_FALLBACK: Category[] = [
  {
    id: 'groceries', name: 'Groceries', color: '#10b981', icon: 'pi pi-shopping-basket', sortOrder: 1, isSystem: true, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: null,
    subCategories: [
      { id: 'mercator', name: 'Mercator', color: '#10b981', icon: 'pi pi-shop', sortOrder: 1, isSystem: false, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: 'groceries', subCategories: [] },
      { id: 'hofer', name: 'Hofer', color: '#34d399', icon: 'pi pi-shop', sortOrder: 2, isSystem: false, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: 'groceries', subCategories: [] }
    ]
  },
  {
    id: 'fuel', name: 'Fuel', color: '#3b82f6', icon: 'pi pi-car', sortOrder: 2, isSystem: true, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: null,
    subCategories: [
      { id: 'omv', name: 'OMV', color: '#60a5fa', icon: 'pi pi-car', sortOrder: 1, isSystem: false, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: 'fuel', subCategories: [] }
    ]
  },
  {
    id: 'subscriptions', name: 'Subscriptions', color: '#8b5cf6', icon: 'pi pi-bolt', sortOrder: 3, isSystem: false, excludeFromExpenses: false, excludeFromIncome: false, parentCategoryId: null,
    subCategories: []
  }
];

@Component({
  standalone: true,
  selector: 'app-categories',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, DialogModule, InputTextModule, TagModule, TooltipModule],
  template: `
    <div class="flex justify-content-end mb-3">
      <p-button label="Add category" icon="pi pi-plus" (onClick)="openCreateDialog()"></p-button>
    </div>

    <div class="grid">
      <div class="col-12 md:col-6 xl:col-4" *ngFor="let category of categories()">
        <p-card>
          <ng-template pTemplate="header">
            <div class="category-header" [style.borderColor]="category.color || 'var(--primary-color)'">
              <div class="flex align-items-center gap-3">
                <span class="category-icon" [style.background]="category.color || 'var(--primary-color)'">
                  <i [class]="category.icon || 'pi pi-folder'"></i>
                </span>
                <div>
                  <div class="text-lg font-semibold">{{ category.name }}</div>
                  <small class="text-color-secondary">{{ category.subCategories.length }} subcategories</small>
                </div>
              </div>
              <div class="flex gap-2 align-items-center">
                <p-tag [value]="category.isSystem ? 'System' : 'Custom'" [severity]="category.isSystem ? 'secondary' : 'success'"></p-tag>
                <p-tag *ngIf="category.excludeFromExpenses" value="Excl. expenses" severity="warn" icon="pi pi-eye-slash"></p-tag>
                <p-tag *ngIf="category.excludeFromIncome" value="Excl. income" severity="warn" icon="pi pi-arrow-circle-up"></p-tag>
              </div>
            </div>
          </ng-template>

          <div class="flex flex-wrap gap-2 mb-3">
            <p-button icon="pi pi-plus" size="small" [text]="true" (onClick)="openCreateDialog(category.id)"></p-button>
            <p-button icon="pi pi-chevron-down" size="small" [text]="true" [severity]="isExpanded(category.id) ? 'contrast' : 'secondary'" (onClick)="toggleExpanded(category.id)"></p-button>
            <p-button icon="pi pi-pencil" size="small" [text]="true" [disabled]="category.isSystem" (onClick)="openEditDialog(category)"></p-button>
            <p-button [icon]="category.excludeFromExpenses ? 'pi pi-eye' : 'pi pi-eye-slash'" size="small" [text]="true" [severity]="category.excludeFromExpenses ? 'warn' : 'secondary'" [pTooltip]="category.excludeFromExpenses ? 'Include in expenses' : 'Exclude from expenses'" tooltipPosition="top" [disabled]="category.isSystem" (onClick)="toggleExcludeFromExpenses(category)"></p-button>
            <p-button [icon]="category.excludeFromIncome ? 'pi pi-arrow-circle-up' : 'pi pi-arrow-up'" size="small" [text]="true" [severity]="category.excludeFromIncome ? 'warn' : 'secondary'" [pTooltip]="category.excludeFromIncome ? 'Include in income' : 'Exclude from income'" tooltipPosition="top" [disabled]="category.isSystem" (onClick)="toggleExcludeFromIncome(category)"></p-button>
            <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" [disabled]="category.isSystem" (onClick)="openDeleteDialog(category)"></p-button>
          </div>

          <div class="text-sm text-color-secondary mb-3">Primary category used for reporting and rule targeting.</div>

          <div *ngIf="isExpanded(category.id); else collapsedPreview" class="subcategories-panel">
            <div class="subcategory-row" *ngFor="let subCategory of category.subCategories">
              <div class="flex align-items-center gap-2">
                <i [class]="subCategory.icon || 'pi pi-tag'" [style.color]="subCategory.color || category.color || 'var(--primary-color)'"></i>
                <span>{{ subCategory.name }}</span>
              </div>
              <div class="flex gap-2">
                <p-button icon="pi pi-pencil" size="small" [text]="true" (onClick)="openEditDialog(subCategory)"></p-button>
                <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" (onClick)="openDeleteDialog(subCategory)"></p-button>
              </div>
            </div>
            <div class="text-sm text-color-secondary" *ngIf="category.subCategories.length === 0">No subcategories yet. Add one for finer-grained rule matching.</div>
          </div>

          <ng-template #collapsedPreview>
            <div class="flex flex-wrap gap-2" *ngIf="category.subCategories.length; else emptyState">
              <p-tag *ngFor="let subCategory of category.subCategories" [value]="subCategory.name" [style]="{ background: subCategory.color || category.color || 'var(--primary-color)', color: '#fff' }"></p-tag>
            </div>
          </ng-template>

          <ng-template #emptyState>
            <span class="text-color-secondary">No subcategories yet.</span>
          </ng-template>
        </p-card>
      </div>
    </div>

    <p-dialog [(visible)]="categoryDialogVisible" [modal]="true" [style]="{ width: 'min(32rem, 95vw)' }" [header]="editingCategoryId ? 'Edit category' : 'Add category'">
      <div class="p-fluid grid mt-1">
        <div class="col-12">
          <label class="field-label" for="categoryName">Name</label>
          <input id="categoryName" pInputText [(ngModel)]="categoryForm.name" />
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="categoryColor">Color</label>
          <div class="flex gap-2 align-items-center">
            <input id="categoryColor" type="color" [(ngModel)]="categoryForm.color" />
            <input pInputText [(ngModel)]="categoryForm.color" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <label class="field-label" for="categoryIcon">Icon</label>
          <input id="categoryIcon" pInputText [(ngModel)]="categoryForm.icon" placeholder="pi pi-tag" />
        </div>
        <div class="col-12">
          <label class="field-label" for="parentCategory">Parent</label>
          <select id="parentCategory" class="native-select" [(ngModel)]="categoryForm.parentCategoryId" [disabled]="editingCategoryId !== null">
            <option [ngValue]="null">Top-level category</option>
            <option *ngFor="let option of parentCategoryOptions()" [ngValue]="option.id">{{ option.name }}</option>
          </select>
          <small class="text-color-secondary" *ngIf="editingCategoryId">Parent can only be set when creating a category.</small>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Cancel" severity="secondary" [outlined]="true" (onClick)="categoryDialogVisible = false"></p-button>
          <p-button label="Save category" icon="pi pi-save" [loading]="savingCategory" (onClick)="saveCategory()"></p-button>
        </div>
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="deleteDialogVisible" [modal]="true" [style]="{ width: 'min(28rem, 95vw)' }" header="Delete category">
      <div class="p-fluid flex flex-column gap-3 mt-1" *ngIf="deleteTargetCategory">
        <p>Delete <strong>{{ deleteTargetCategory.name }}</strong> and reassign existing transactions and rules.</p>
        <div>
          <label class="field-label" for="reassignCategory">Reassign to</label>
          <select id="reassignCategory" class="native-select" [(ngModel)]="reassignCategoryId">
            <option value="" disabled>Select reassignment target</option>
            <option *ngFor="let option of reassignCategoryOptions()" [value]="option.id">{{ option.name }}</option>
          </select>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Cancel" severity="secondary" [outlined]="true" (onClick)="deleteDialogVisible = false"></p-button>
          <p-button label="Delete and reassign" icon="pi pi-trash" severity="danger" [loading]="deletingCategory" (onClick)="deleteCategory()"></p-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .category-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem 1.25rem 0;
      border-top: 4px solid transparent;
    }

    .category-icon {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 999px;
      color: #fff;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }

    .subcategories-panel {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-top: 1rem;
    }

    .subcategory-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.75rem 0;
      border-top: 1px solid var(--surface-border);
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
export class CategoriesComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly categories = signal<Category[]>(CATEGORIES_FALLBACK);
  protected readonly parentCategoryOptions = computed(() => this.categories().filter((category) => !category.parentCategoryId));
  protected readonly reassignCategoryOptions = computed(() => {
    if (!this.deleteTargetCategory) {
      return this.flattenCategories(this.categories());
    }

    const blockedIds = new Set(this.collectCategoryIds(this.deleteTargetCategory));
    return this.flattenCategories(this.categories()).filter((category) => !blockedIds.has(category.id));
  });

  protected categoryDialogVisible = false;
  protected deleteDialogVisible = false;
  protected savingCategory = false;
  protected deletingCategory = false;
  protected editingCategoryId: string | null = null;
  protected deleteTargetCategory: Category | null = null;
  protected reassignCategoryId = '';
  protected categoryForm: CategoryFormModel = this.createEmptyCategoryForm();

  private readonly expandedCategoryIds = new Set<string>();

  constructor() {
    this.loadCategories();
  }

  protected isExpanded(categoryId: string): boolean {
    return this.expandedCategoryIds.has(categoryId);
  }

  protected toggleExpanded(categoryId: string): void {
    if (this.expandedCategoryIds.has(categoryId)) {
      this.expandedCategoryIds.delete(categoryId);
    } else {
      this.expandedCategoryIds.add(categoryId);
    }
  }

  protected openCreateDialog(parentCategoryId: string | null = null): void {
    this.editingCategoryId = null;
    this.categoryForm = this.createEmptyCategoryForm(parentCategoryId);
    this.categoryDialogVisible = true;
  }

  protected openEditDialog(category: Category): void {
    if (category.isSystem) {
      return;
    }

    this.editingCategoryId = category.id;
    this.categoryForm = {
      name: category.name,
      color: category.color ?? '#64748b',
      icon: category.icon ?? 'pi pi-tag',
      parentCategoryId: category.parentCategoryId ?? null
    };
    this.categoryDialogVisible = true;
  }

  protected saveCategory(): void {
    if (!this.categoryForm.name.trim()) {
      this.messageService.add({ severity: 'warn', summary: 'Name required', detail: 'Provide a category name before saving.' });
      return;
    }

    this.savingCategory = true;
    const createPayload: CreateCategoryRequest = {
      name: this.categoryForm.name.trim(),
      color: this.categoryForm.color,
      icon: this.categoryForm.icon,
      parentCategoryId: this.categoryForm.parentCategoryId
    };

    const request$ = this.editingCategoryId
      ? this.apiService.updateCategory(this.editingCategoryId, {
          name: createPayload.name,
          color: createPayload.color,
          icon: createPayload.icon
        } as UpdateCategoryRequest)
      : this.apiService.createCategory(createPayload);

    request$.pipe(
      catchError(() => of(this.buildLocalCategory(createPayload, this.editingCategoryId))),
      finalize(() => this.savingCategory = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((category) => {
      this.upsertCategory(category);
      this.categoryDialogVisible = false;
      this.messageService.add({
        severity: 'success',
        summary: this.editingCategoryId ? 'Category updated' : 'Category created',
        detail: 'Category hierarchy is up to date.'
      });
      this.editingCategoryId = null;
    });
  }

  protected openDeleteDialog(category: Category): void {
    if (category.isSystem) {
      return;
    }

    this.deleteTargetCategory = category;
    this.reassignCategoryId = this.reassignCategoryOptions()[0]?.id ?? '';
    this.deleteDialogVisible = true;
  }

  protected toggleExcludeFromIncome(category: Category): void {
    const newValue = !category.excludeFromIncome;
    this.apiService.updateCategory(category.id, { excludeFromIncome: newValue } as UpdateCategoryRequest).pipe(
      catchError(() => of({ ...category, excludeFromIncome: newValue } as Category)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((updated) => {
      this.upsertCategory(updated);
      this.messageService.add({
        severity: newValue ? 'warn' : 'success',
        summary: newValue ? 'Excluded from income' : 'Included in income',
        detail: `"${category.name}" will ${newValue ? 'no longer' : 'now'} count toward income totals and reports.`
      });
    });
  }

  protected toggleExcludeFromExpenses(category: Category): void {
    const newValue = !category.excludeFromExpenses;
    this.apiService.updateCategory(category.id, { excludeFromExpenses: newValue } as UpdateCategoryRequest).pipe(
      catchError(() => of({ ...category, excludeFromExpenses: newValue } as Category)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((updated) => {
      this.upsertCategory(updated);
      this.messageService.add({
        severity: newValue ? 'warn' : 'success',
        summary: newValue ? 'Excluded from expenses' : 'Included in expenses',
        detail: `"${category.name}" will ${newValue ? 'no longer' : 'now'} count toward expense totals and reports.`
      });
    });
  }

  protected deleteCategory(): void {
    if (!this.deleteTargetCategory || !this.reassignCategoryId) {
      this.messageService.add({ severity: 'warn', summary: 'Choose target', detail: 'Select a reassignment category first.' });
      return;
    }

    this.deletingCategory = true;
    this.apiService.deleteCategory(this.deleteTargetCategory.id, { reassignToCategoryId: this.reassignCategoryId }).pipe(
      catchError(() => of(void 0)),
      finalize(() => this.deletingCategory = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.removeCategory(this.deleteTargetCategory!.id);
      this.deleteDialogVisible = false;
      this.messageService.add({ severity: 'success', summary: 'Category deleted', detail: 'Transactions and rules were reassigned.' });
      this.deleteTargetCategory = null;
    });
  }

  private loadCategories(): void {
    this.apiService.getCategories().pipe(
      catchError(() => of(CATEGORIES_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((categories) => this.categories.set(categories));
  }

  private upsertCategory(category: Category): void {
    this.categories.update((categories) => {
      const nextCategories = structuredClone(categories);
      const replaceCategory = (items: Category[]): boolean => {
        for (let index = 0; index < items.length; index += 1) {
          if (items[index].id === category.id) {
            items[index] = { ...items[index], ...category, subCategories: category.subCategories ?? items[index].subCategories };
            return true;
          }

          if (replaceCategory(items[index].subCategories)) {
            return true;
          }
        }

        return false;
      };

      if (replaceCategory(nextCategories)) {
        return nextCategories;
      }

      if (category.parentCategoryId) {
        const parent = this.findCategory(category.parentCategoryId, nextCategories);
        if (parent) {
          parent.subCategories = [...parent.subCategories, category].sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
          this.expandedCategoryIds.add(parent.id);
          return nextCategories;
        }
      }

      return [...nextCategories, category].sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
    });
  }

  private removeCategory(categoryId: string): void {
    this.categories.update((categories) => {
      const removeFrom = (items: Category[]): Category[] => items
        .filter((category) => category.id !== categoryId)
        .map((category) => ({ ...category, subCategories: removeFrom(category.subCategories) }));

      return removeFrom(categories);
    });
  }

  private buildLocalCategory(payload: CreateCategoryRequest, id?: string | null): Category {
    return {
      id: id ?? `local-${Date.now()}`,
      name: payload.name,
      color: payload.color,
      icon: payload.icon,
      sortOrder: 99,
      isSystem: false,
      excludeFromExpenses: false,
      excludeFromIncome: false,
      parentCategoryId: payload.parentCategoryId ?? null,
      subCategories: []
    };
  }

  private createEmptyCategoryForm(parentCategoryId: string | null = null): CategoryFormModel {
    return {
      name: '',
      color: '#64748b',
      icon: 'pi pi-tag',
      parentCategoryId
    };
  }

  private flattenCategories(categories: Category[]): Category[] {
    return categories.flatMap((category) => [category, ...this.flattenCategories(category.subCategories ?? [])]);
  }

  private collectCategoryIds(category: Category): string[] {
    return [category.id, ...category.subCategories.flatMap((item) => this.collectCategoryIds(item))];
  }

  private findCategory(categoryId: string, categories: Category[] = this.categories()): Category | undefined {
    return this.flattenCategories(categories).find((category) => category.id === categoryId);
  }
}

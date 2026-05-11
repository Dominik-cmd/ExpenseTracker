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

import { EMPTY_CATEGORIES } from '../../core/constants/fallbacks';
import { Category, CreateCategoryRequest, UpdateCategoryRequest } from '../../core/models';
import { CategoryService } from '../../core/services/category.service';

interface CategoryFormModel {
  name: string;
  color: string;
  icon: string;
  parentCategoryId: string | null;
}

@Component({
  standalone: true,
  selector: 'app-categories',
  imports: [FormsModule, ButtonModule, CardModule, DialogModule, InputTextModule, TagModule, TooltipModule],
  template: `
    <div class="flex justify-content-end mb-3">
      <p-button label="Add category" icon="pi pi-plus" (onClick)="openCreateDialog()"></p-button>
    </div>

    <div class="grid">
      @for (category of categories(); track category.id) {
        <div class="col-12 md:col-6 xl:col-4">
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
                  @if (category.excludeFromExpenses) {
                    <p-tag value="Excl. expenses" severity="warn" icon="pi pi-eye-slash"></p-tag>
                  }
                  @if (category.excludeFromIncome) {
                    <p-tag value="Excl. income" severity="warn" icon="pi pi-arrow-circle-up"></p-tag>
                  }
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

            @if (isExpanded(category.id)) {
              <div class="subcategories-panel">
                @for (subCategory of category.subCategories; track subCategory.id) {
                  <div class="subcategory-row">
                    <div class="flex align-items-center gap-2">
                      <i [class]="subCategory.icon || 'pi pi-tag'" [style.color]="subCategory.color || category.color || 'var(--primary-color)'"></i>
                      <span>{{ subCategory.name }}</span>
                      @if (subCategory.excludeFromIncome) {
                        <p-tag value="Excl. income" severity="warn" icon="pi pi-arrow-circle-up" [style]="{ fontSize: '0.7rem' }"></p-tag>
                      }
                    </div>
                    <div class="flex gap-2">
                      <p-button [icon]="subCategory.excludeFromIncome ? 'pi pi-arrow-circle-up' : 'pi pi-arrow-up'" size="small" [text]="true" [severity]="subCategory.excludeFromIncome ? 'warn' : 'secondary'" [pTooltip]="subCategory.excludeFromIncome ? 'Include in income' : 'Exclude from income'" tooltipPosition="top" (onClick)="toggleExcludeFromIncome(subCategory)"></p-button>
                      <p-button icon="pi pi-pencil" size="small" [text]="true" (onClick)="openEditDialog(subCategory)"></p-button>
                      <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" (onClick)="openDeleteDialog(subCategory)"></p-button>
                    </div>
                  </div>
                } @empty {
                  <div class="text-sm text-color-secondary">No subcategories yet. Add one for finer-grained rule matching.</div>
                }
              </div>
            } @else {
              @if (category.subCategories.length) {
                <div class="flex flex-wrap gap-2">
                  @for (subCategory of category.subCategories; track subCategory.id) {
                    <p-tag [value]="subCategory.name" [style]="{ background: subCategory.color || category.color || 'var(--primary-color)', color: '#fff' }"></p-tag>
                  }
                </div>
              } @else {
                <span class="text-color-secondary">No subcategories yet.</span>
              }
            }
          </p-card>
        </div>
      }
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
            @for (option of parentCategoryOptions(); track option.id) {
              <option [ngValue]="option.id">{{ option.name }}</option>
            }
          </select>
          @if (editingCategoryId) {
            <small class="text-color-secondary">Parent can only be set when creating a category.</small>
          }
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
      @if (deleteTargetCategory; as target) {
        <div class="p-fluid flex flex-column gap-3 mt-1">
          <p>Delete <strong>{{ target.name }}</strong> and reassign existing transactions and rules.</p>
          <div>
            <label class="field-label" for="reassignCategory">Reassign to</label>
            <select id="reassignCategory" class="native-select" [(ngModel)]="reassignCategoryId">
              <option value="" disabled>Select reassignment target</option>
              @for (option of reassignCategoryOptions(); track option.id) {
                <option [value]="option.id">{{ option.name }}</option>
              }
            </select>
          </div>
        </div>
      }

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
  private readonly categoryService = inject(CategoryService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly categories = signal<Category[]>(EMPTY_CATEGORIES);
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
      ? this.categoryService.updateCategory(this.editingCategoryId, {
          name: createPayload.name,
          color: createPayload.color,
          icon: createPayload.icon
        } as UpdateCategoryRequest)
      : this.categoryService.createCategory(createPayload);

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
    this.categoryService.updateCategory(category.id, { excludeFromIncome: newValue } as UpdateCategoryRequest).pipe(
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
    this.categoryService.updateCategory(category.id, { excludeFromExpenses: newValue } as UpdateCategoryRequest).pipe(
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
    this.categoryService.deleteCategory(this.deleteTargetCategory.id, { reassignToCategoryId: this.reassignCategoryId }).pipe(
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
    this.categoryService.getCategories().pipe(
      catchError(() => of(EMPTY_CATEGORIES)),
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

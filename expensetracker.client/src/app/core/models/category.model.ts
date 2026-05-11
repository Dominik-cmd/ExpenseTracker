export interface Category {
  id: string;
  name: string;
  color?: string | null;
  icon?: string | null;
  sortOrder: number;
  isSystem: boolean;
  excludeFromExpenses: boolean;
  excludeFromIncome: boolean;
  parentCategoryId?: string | null;
  subCategories: Category[];
}

export interface CreateCategoryRequest {
  name: string;
  color?: string | null;
  icon?: string | null;
  parentCategoryId?: string | null;
}

export interface UpdateCategoryRequest {
  name?: string | null;
  color?: string | null;
  icon?: string | null;
  sortOrder?: number | null;
  excludeFromExpenses?: boolean | null;
  excludeFromIncome?: boolean | null;
}

export interface DeleteCategoryRequest {
  reassignToCategoryId: string;
}

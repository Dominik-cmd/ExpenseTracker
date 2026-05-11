import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import {
  Category,
  CreateCategoryRequest,
  DeleteCategoryRequest,
  UpdateCategoryRequest
} from '../models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(buildApiUrl('/api/categories'));
  }

  createCategory(payload: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>(buildApiUrl('/api/categories'), payload);
  }

  updateCategory(id: string, payload: UpdateCategoryRequest): Observable<Category> {
    return this.http.patch<Category>(buildApiUrl(`/api/categories/${id}`), payload);
  }

  deleteCategory(id: string, payload: DeleteCategoryRequest): Observable<void> {
    return this.http.request<void>('DELETE', buildApiUrl(`/api/categories/${id}`), { body: payload });
  }
}

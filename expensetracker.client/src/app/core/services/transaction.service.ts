import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import {
  BulkRecategorizeRequest,
  CreateTransactionRequest,
  PagedResult,
  RecategorizeTransactionRequest,
  Transaction,
  TransactionFilters,
  UpdateTransactionRequest
} from '../models';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  private readonly http = inject(HttpClient);

  getTransactions(filters: TransactionFilters = {}): Observable<PagedResult<Transaction>> {
    return this.http.get<PagedResult<Transaction>>(buildApiUrl('/api/transactions'), {
      params: this.createParams(filters)
    });
  }

  getTransaction(id: string): Observable<Transaction> {
    return this.http.get<Transaction>(buildApiUrl(`/api/transactions/${id}`));
  }

  createTransaction(payload: CreateTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>(buildApiUrl('/api/transactions'), payload);
  }

  updateTransaction(id: string, payload: UpdateTransactionRequest): Observable<Transaction> {
    return this.http.patch<Transaction>(buildApiUrl(`/api/transactions/${id}`), payload);
  }

  deleteTransaction(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/transactions/${id}`));
  }

  recategorizeTransaction(id: string, payload: RecategorizeTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>(buildApiUrl(`/api/transactions/${id}/recategorize`), payload);
  }

  bulkRecategorize(payload: BulkRecategorizeRequest): Observable<void> {
    return this.http.post<void>(buildApiUrl('/api/transactions/bulk-recategorize'), payload);
  }

  exportTransactions(filters: TransactionFilters = {}): Observable<Blob> {
    return this.http.get(buildApiUrl('/api/transactions/export'), {
      params: this.createParams(filters),
      responseType: 'blob'
    });
  }

  private createParams(values: object): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(values)) {
      if (value === undefined || value === null || value === '') continue;
      if (Array.isArray(value)) {
        for (const item of value) {
          if (item !== undefined && item !== null && item !== '') {
            params = params.append(key, String(item));
          }
        }
        continue;
      }
      params = params.set(key, String(value));
    }
    return params;
  }
}
